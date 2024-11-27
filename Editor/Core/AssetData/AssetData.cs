using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    static class AssetDataExtension
    {
        public static bool IsTheSame(this BaseAssetData assetData, BaseAssetData other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(assetData, other))
            {
                return true;
            }

            if (other.GetType() != assetData.GetType())
            {
                return false;
            }

            return assetData.Name == other.Name
                && assetData.Identifier.Equals(other.Identifier)
                && assetData.AssetType == other.AssetType
                && assetData.Status == other.Status
                && assetData.Updated == other.Updated
                && assetData.Created == other.Created
                && assetData.Tags.SequenceEqual(other.Tags)
                && assetData.Description == other.Description
                && assetData.CreatedBy == other.CreatedBy
                && assetData.UpdatedBy == other.UpdatedBy
                && assetData.SourceFiles.SequenceEqual(other.SourceFiles);
        }
    }

    [Serializable]
    class AssetData : BaseAssetData
    {
        static readonly int s_MaxThumbnailSize = 180;

        [SerializeField]
        AssetIdentifier m_Identifier;

        [SerializeField]
        int m_SequenceNumber;

        [SerializeField]
        int m_ParentSequenceNumber;

        [SerializeField]
        string m_Changelog;

        [SerializeField]
        string m_Name;

        [SerializeField]
        AssetType m_AssetType;

        [SerializeField]
        string m_Status;

        [SerializeField]
        string m_Description;

        [SerializeField]
        long m_Created;

        [SerializeField]
        long m_Updated;

        [SerializeField]
        string m_CreatedBy;

        [SerializeField]
        string m_UpdatedBy;

        [SerializeField]
        string m_PreviewFilePath;

        [SerializeField]
        bool m_IsFrozen;

        [SerializeField]
        List<string> m_Tags;

        [SerializeField]
        List<AssetIdentifier> m_Dependencies = new();

        [SerializeField]
        string m_ThumbnailUrl;

        [SerializeField]
        bool m_ThumbnailProcessed; // Flag to avoid multiple requests for assets with no thumbnail

        [SerializeField]
        bool m_PrimaryExtensionProcessed; // Flag to avoid multiple requests for assets with no primary extension

        [SerializeReference]
        List<BaseAssetDataFile> m_UVCSFiles = new();

        [SerializeReference]
        List<BaseAssetData> m_Versions = new();

        [SerializeReference]
        List<IMetadata> m_Metadata = new();

        Task<Uri> m_GetPreviewStatusTask;
        Task<AssetComparisonResult> m_PreviewStatusTask;
        Task m_PrimaryExtensionTask;
        Task m_RefreshPropertiesTask;
        Task m_RefreshDependenciesTask;
        Task m_RefreshVersionsTask;
        Task m_ThumbnailUrlTask;

        IThumbnailDownloader m_ThumbnailDownloader;

        public override AssetIdentifier Identifier => m_Identifier;
        public override int SequenceNumber => m_SequenceNumber;
        public override int ParentSequenceNumber => m_ParentSequenceNumber;
        public override string Changelog => m_Changelog;
        public override string Name => m_Name;
        public override AssetType AssetType => m_AssetType;
        public override string Status => m_Status;
        public override string Description => m_Description;
        public override DateTime? Created => new DateTime(m_Created, DateTimeKind.Utc);
        public override DateTime? Updated => new DateTime(m_Updated, DateTimeKind.Utc);
        public override string CreatedBy => m_CreatedBy;
        public override string UpdatedBy => m_UpdatedBy;
        public string PreviewFilePath => m_PreviewFilePath;
        public bool IsFrozen => m_IsFrozen;
        public override IEnumerable<string> Tags => m_Tags;
        public override IEnumerable<AssetIdentifier> Dependencies => m_Dependencies;
        public override IEnumerable<BaseAssetDataFile> UVCSFiles => m_UVCSFiles;

        public override List<IMetadata> Metadata
        {
            get => m_Metadata;
            set => m_Metadata = value;
        }

        public override IEnumerable<BaseAssetData> Versions => m_Versions;

        IThumbnailDownloader ThumbnailDownloader =>
            m_ThumbnailDownloader ??= ServicesContainer.instance.Resolve<IThumbnailDownloader>();

        public AssetData()
        {
        }

#pragma warning disable S107 // Disabling the warning regarding too many parameters.
        public AssetData(AssetIdentifier assetIdentifier,
            int sequenceNumber,
            int parentSequenceNumber,
            string changelog,
            string name,
            AssetType assetType,
            string status,
            string description,
            DateTime created,
            DateTime updated,
            string createdBy,
            string updatedBy,
            string previewFilePath,
            bool isFrozen,
            IEnumerable<string> tags)
        {
            m_Identifier = assetIdentifier;
            m_SequenceNumber = sequenceNumber;
            m_ParentSequenceNumber = parentSequenceNumber;
            m_Changelog = changelog;
            m_Name = name;
            m_AssetType = assetType;
            m_Status = status;
            m_Description = description;
            m_Created = created.Ticks;
            m_Updated = updated.Ticks;
            m_CreatedBy = createdBy;
            m_UpdatedBy = updatedBy;
            m_PreviewFilePath = previewFilePath;
            m_IsFrozen = isFrozen;
            m_Tags = tags?.ToList() ?? new List<string>();
        }
#pragma warning restore S107

#pragma warning disable S107  // Disabling the warning regarding too many parameters.
        // Used when de-serialized from version 1.0 to fill data not in the IAsset
        public void FillFromPersistenceLegacy(IEnumerable<AssetIdentifier> dependencyAssets,
            AssetComparisonResult assetComparisonResult,
            string thumbnailUrl,
            IEnumerable<BaseAssetDataFile> sourceFiles,
            BaseAssetDataFile primarySourceFile,
            IEnumerable<BaseAssetDataFile> uvcsFiles)
        {
            m_Dependencies = dependencyAssets?.ToList();
            m_ThumbnailUrl = thumbnailUrl;
            m_UVCSFiles = uvcsFiles?.ToList();

            SourceFiles = sourceFiles?.ToList();
            PreviewStatus = new List<AssetDataStatusType> { StatusTypeFromComparisonResult(assetComparisonResult) };
        }
#pragma warning restore S107

#pragma warning disable S107 // Disabling the warning regarding too many parameters.
        // Used when de-serialized from version 2.0
        public void FillFromPersistence(AssetIdentifier assetIdentifier,
            int sequenceNumber,
            int parentSequenceNumber,
            string changelog,
            string name,
            AssetType assetType,
            string status,
            string description,
            DateTime created,
            DateTime updated,
            string createdBy,
            string updatedBy,
            string previewFilePath,
            bool isFrozen,
            IEnumerable<string> tags,
            IEnumerable<AssetDataFile> sourceFiles,
            IEnumerable<AssetIdentifier> dependencies,
            IEnumerable<IMetadata> metadata)
        {
            m_Identifier = assetIdentifier;
            m_SequenceNumber = sequenceNumber;
            m_ParentSequenceNumber = parentSequenceNumber;
            m_Changelog = changelog;
            m_Name = name;
            m_AssetType = assetType;
            m_Status = status;
            m_Description = description;
            m_Created = created.Ticks;
            m_Updated = updated.Ticks;
            m_CreatedBy = createdBy;
            m_UpdatedBy = updatedBy;
            m_PreviewFilePath = previewFilePath;
            m_IsFrozen = isFrozen;
            m_Tags = tags.ToList();

            m_Dependencies = dependencies?.ToList();
            m_Metadata = metadata?.ToList();

            SourceFiles = sourceFiles?.Cast<BaseAssetDataFile>().ToList();
        }
#pragma warning restore S107

        void FillFromOther(AssetData other)
        {
            m_Identifier = other.Identifier;
            m_SequenceNumber = other.SequenceNumber;
            m_ParentSequenceNumber = other.ParentSequenceNumber;
            m_Changelog = other.Changelog;
            m_Name = other.Name;
            m_AssetType = other.AssetType;
            m_Status = other.Status;
            m_Description = other.Description;
            m_Created = other.Created?.Ticks ?? 0;
            m_Updated = other.Updated?.Ticks ?? 0;
            m_CreatedBy = other.CreatedBy;
            m_UpdatedBy = other.UpdatedBy;
            m_PreviewFilePath = other.PreviewFilePath;
            m_IsFrozen = other.IsFrozen;
            m_Tags = other.Tags?.ToList() ?? new List<string>();
            m_Metadata = other.Metadata?.ToList() ?? new List<IMetadata>();
        }

        public override async Task GetThumbnailAsync(Action<AssetIdentifier, Texture2D> callback = null,
            CancellationToken token = default)
        {
            // Because a AssetData is tight to a version, and preview modification creates a new version,
            // we can assume that the thumbnail is always the same.
            if (Thumbnail != null || m_ThumbnailProcessed)
            {
                callback?.Invoke(Identifier, Thumbnail);
                return;
            }

            // Look inside the cache before making any request
            var cachedThumbnail = ThumbnailDownloader.GetCachedThumbnail(Identifier);
            if (cachedThumbnail != null)
            {
                SetThumbnailAndInvokeCallback(cachedThumbnail, callback);
                return;
            }

            m_ThumbnailUrlTask ??= GetThumbnailUrlAsync(token);

            try
            {
                await m_ThumbnailUrlTask;
            }
            finally
            {
                m_ThumbnailUrlTask = null;
            }

            ThumbnailDownloader.DownloadThumbnail(Identifier, m_ThumbnailUrl,
                (_, texture) =>
                {
                    SetThumbnailAndInvokeCallback(texture, callback);
                });
        }

        void SetThumbnailAndInvokeCallback(Texture2D texture, Action<AssetIdentifier, Texture2D> callback)
        {
            Thumbnail = texture;
            m_ThumbnailProcessed = true;
            callback?.Invoke(Identifier, Thumbnail);
        }

        public override async Task GetPreviewStatusAsync(
            Action<AssetIdentifier, IEnumerable<AssetDataStatusType>> callback = null,
            CancellationToken token = default)
        {
            if (PreviewStatus != null && PreviewStatus.Any())
            {
                callback?.Invoke(Identifier, PreviewStatus);
                return;
            }

            if (m_PreviewStatusTask == null)
            {
                var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
                var unityConnectProxy = ServicesContainer.instance.Resolve<IUnityConnectProxy>();
                var assetsSdkProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
                if (assetsSdkProvider.AuthenticationState == AuthenticationState.LoggedIn &&
                    unityConnectProxy.AreCloudServicesReachable &&
                    assetDataManager.IsInProject(Identifier))
                {
                    m_PreviewStatusTask = ServicesContainer.instance.Resolve<IAssetsProvider>()
                        .CompareAssetWithCloudAsync(this, token);
                }
            }

            if (m_PreviewStatusTask != null)
            {
                var assetComparisonResult = await m_PreviewStatusTask;
                PreviewStatus = new[] { StatusTypeFromComparisonResult(assetComparisonResult) };
                m_PreviewStatusTask = null;
            }

            callback?.Invoke(Identifier, PreviewStatus);
        }

        static AssetDataStatusType StatusTypeFromComparisonResult(AssetComparisonResult result)
        {
            return result switch
            {
                AssetComparisonResult.UpToDate => AssetDataStatusType.UpToDate,
                AssetComparisonResult.OutDated => AssetDataStatusType.OutOfDate,
                AssetComparisonResult.NotFoundOrInaccessible => AssetDataStatusType.Error,
                AssetComparisonResult.Unknown => AssetDataStatusType.Imported,
                _ => AssetDataStatusType.None
            };
        }

        public override async Task ResolvePrimaryExtensionAsync(Action<AssetIdentifier, string> callback = null,
            CancellationToken token = default)
        {
            // Because an AssetData is tight to a version, and files modification creates a new version,
            // we can assume that the primary extension is always the same.
            if (m_PrimaryExtensionProcessed || !string.IsNullOrEmpty(PrimaryExtension))
            {
                callback?.Invoke(Identifier, PrimaryExtension);
                return;
            }

            m_PrimaryExtensionTask ??= ResolvePrimaryExtensionInternalAsync(token);

            try
            {
                await m_PrimaryExtensionTask;
                m_PrimaryExtensionProcessed = true;
            }
            catch (ForbiddenException)
            {
                // Ignore if the Asset is unavailable
            }
            finally
            {
                m_PrimaryExtensionTask = null;
            }

            callback?.Invoke(Identifier, PrimaryExtension);
        }

        async Task GetThumbnailUrlAsync(CancellationToken token)
        {
            var assetsSdkProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();

            m_GetPreviewStatusTask ??= assetsSdkProvider.GetPreviewUrlAsync(this, s_MaxThumbnailSize, token);

            Uri previewFileUrl = null;

            try
            {
                previewFileUrl = await m_GetPreviewStatusTask;
            }
            catch (NotFoundException)
            {
                // Ignore if the Asset is not found
            }
            catch (ForbiddenException)
            {
                // Ignore if the Asset is unavailable
            }
            catch (HttpRequestException)
            {
                // Ignore unreachable host
            }
            finally
            {
                m_GetPreviewStatusTask = null;
            }

            m_ThumbnailUrl = previewFileUrl?.ToString() ?? string.Empty;
        }

        public override async Task RefreshPropertiesAsync(CancellationToken token = default)
        {
            m_RefreshPropertiesTask ??= RefreshPropertiesInternalAsync(token);
            try
            {
                await m_RefreshPropertiesTask;
            }
            catch (HttpRequestException)
            {
                // Ignore unreachable host
            }
            finally
            {
                m_RefreshPropertiesTask = null;
            }
        }

        async Task RefreshPropertiesInternalAsync(CancellationToken token = default)
        {
            var assetsSdkProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
            var updatedAsset = await assetsSdkProvider.GetAssetAsync(Identifier, token);

            FillFromOther(updatedAsset);
        }

        async Task ResolvePrimaryExtensionInternalAsync(CancellationToken token = default)
        {
            try
            {
                var files = new List<BaseAssetDataFile>();

                var assetsSdkProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();

                await foreach (var file in assetsSdkProvider.ListFilesAsync(this, Range.All, token))
                {
                    files.Add(file);
                }

                token.ThrowIfCancellationRequested();

                SourceFiles = files;
            }
            catch (HttpRequestException)
            {
                // Ignore unreachable host
            }
        }

        public override async Task RefreshDependenciesAsync(CancellationToken token = default)
        {
            m_RefreshDependenciesTask ??= RefreshDependenciesInternalAsync(token);
            try
            {
                await m_RefreshDependenciesTask;
            }
            catch (HttpRequestException)
            {
                // Ignore unreachable host
            }
            finally
            {
                m_RefreshDependenciesTask = null;
            }
        }

        async Task RefreshDependenciesInternalAsync(CancellationToken token)
        {
            var dependencies = new List<AssetIdentifier>();
            await foreach (var dependency in AssetDataDependencyHelper.LoadDependenciesAsync(this, token))
            {
                dependencies.Add(dependency);
            }

            m_Dependencies = dependencies;
        }

        public override async Task RefreshVersionsAsync(CancellationToken token = default)
        {
            m_RefreshVersionsTask ??= RefreshVersionsInternalAsync(token);
            try
            {
                await m_RefreshVersionsTask;
            }
            catch (HttpRequestException)
            {
                // Ignore unreachable host
            }
            finally
            {
                m_RefreshVersionsTask = null;
            }
        }

        async Task RefreshVersionsInternalAsync(CancellationToken token)
        {
            var assetsSdkProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
            var versions = new List<BaseAssetData>();
            try
            {
                await foreach (var assetData in assetsSdkProvider.ListVersionInDescendingOrderAsync(m_Identifier, token))
                {
                    versions.Add(assetData);
                    assetData.m_Versions = versions;
                }
            }
            catch (NotFoundException)
            {
                versions.Clear();
            }

            m_Versions = versions;
        }
    }
}
