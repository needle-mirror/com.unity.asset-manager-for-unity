using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;
using UnityEngine;
using Task = System.Threading.Tasks.Task;

namespace Unity.AssetManager.Editor
{
    interface IAssetData
    {
        string Name { get; }
        AssetIdentifier Identifier { get; }
        int SequenceNumber { get; }
        int ParentSequenceNumber { get; }
        string Changelog { get; }
        AssetType AssetType { get; }
        string Status { get; }
        DateTime? Updated { get; }
        DateTime? Created { get; }
        IEnumerable<string> Tags { get; }
        string Description { get; }
        string CreatedBy { get; }
        string UpdatedBy { get; }
        IAssetDataFile PrimarySourceFile { get; }
        IEnumerable<IAssetDataFile> SourceFiles { get; }
        IEnumerable<AssetPreview.IStatus> PreviewStatus { get; }
        IEnumerable<DependencyAsset> Dependencies { get; }
        IEnumerable<IAssetDataFile> UVCSFiles { get; }
        IEnumerable<IAssetData> Versions { get; }

        Task GetThumbnailAsync(Action<AssetIdentifier, Texture2D> callback = null, CancellationToken token = default);
        Task ResolvePrimaryExtensionAsync(Action<AssetIdentifier, string> callback, CancellationToken token = default);
        Task SyncWithCloudAsync(Action<AssetIdentifier> callback, CancellationToken token = default);
        Task SyncWithCloudLatestAsync(Action<AssetIdentifier> callback, CancellationToken token = default);

        Task GetPreviewStatusAsync(Action<AssetIdentifier, IEnumerable<AssetPreview.IStatus>> callback = null,
            CancellationToken token =
                default); // TODO Move away all methods not related to the actual IAssetData raw data

        Task RefreshVersionsAsync(CancellationToken token = default);
    }

    static class AssetDataExtension
    {
        public static bool IsTheSame(this IAssetData assetData, IAssetData other)
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
    class AssetData : IAssetData
    {
        public static readonly string NoPrimaryExtension = "unknown";

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
        bool m_IsFrozen;

        [SerializeField]
        List<string> m_Tags;

        [SerializeField]
        List<DependencyAsset> m_DependencyAssets = new();

        [SerializeField]
        AssetComparisonResult m_AssetComparisonResult = AssetComparisonResult.None;

        [SerializeField]
        string m_ThumbnailUrl;

        [SerializeReference]
        List<IAssetDataFile> m_SourceFiles = new();

        [SerializeReference]
        IAssetDataFile m_PrimarySourceFile;

        [SerializeReference]
        List<IAssetDataFile> m_UVCSFiles = new();

        [SerializeReference]
        List<IAssetData> m_Versions = new();

        Task<Uri> m_GetPreviewStatusTask;
        Task<AssetComparisonResult> m_PreviewStatusTask;
        Task m_PrimaryExtensionTask;
        Task m_RefreshFilesTask;
        Task m_SyncWithCloudTask;
        Task m_SyncWithCloudLatestTask;

        public AssetIdentifier Identifier => m_Identifier;
        public int SequenceNumber => m_SequenceNumber;
        public int ParentSequenceNumber => m_ParentSequenceNumber;
        public string Changelog => m_Changelog;
        public string Name => m_Name;
        public AssetType AssetType => m_AssetType;
        public string Status => m_Status;
        public string Description => m_Description;
        public DateTime? Created => new DateTime(m_Created, DateTimeKind.Utc);
        public DateTime? Updated => new DateTime(m_Updated, DateTimeKind.Utc);
        public string CreatedBy => m_CreatedBy;
        public string UpdatedBy => m_UpdatedBy;
        public bool IsFrozen => m_IsFrozen;
        public IEnumerable<string> Tags => m_Tags;
        public IAssetDataFile PrimarySourceFile => m_PrimarySourceFile;
        public IEnumerable<IAssetDataFile> SourceFiles => m_SourceFiles;
        public IEnumerable<DependencyAsset> Dependencies => m_DependencyAssets;
        public IEnumerable<IAssetDataFile> UVCSFiles => m_UVCSFiles;

        public string ThumbnailUrl => m_ThumbnailUrl; // Used by persistence only to get the current state
                                                      // Other normal usage should use GetThumbnailAsync

        public IEnumerable<AssetPreview.IStatus> PreviewStatus
        {
            get
            {
                AssetPreview.IStatus s = null;
                switch (m_AssetComparisonResult)
                {
                    case AssetComparisonResult.UpToDate:
                        s = AssetDataStatus.UpToDate;
                        break;
                    case AssetComparisonResult.OutDated:
                        s = AssetDataStatus.OutOfDate;
                        break;
                    case AssetComparisonResult.NotFoundOrInaccessible:
                        s = AssetDataStatus.Error;
                        break;
                    case AssetComparisonResult.Unknown:
                        s = AssetDataStatus.Imported;
                        break;
                }

                return new[] { s };
            }
        }

        public IEnumerable<IAssetData> Versions => m_Versions;

        public AssetData(){}

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
            m_IsFrozen = isFrozen;
            m_Tags = tags.ToList();
        }
        #pragma warning restore S107

        #pragma warning disable S107  // Disabling the warning regarding too many parameters.
        // Used when de-serialized from version 1.0 to fill data not in the IAsset
        public void FillFromPersistenceLegacy(IEnumerable<DependencyAsset> dependencyAssets,
            AssetComparisonResult assetComparisonResult,
            string thumbnailUrl,
            IEnumerable<IAssetDataFile> sourceFiles,
            IAssetDataFile primarySourceFile,
            IEnumerable<IAssetDataFile> uvcsFiles,
            IEnumerable<IAssetData> versions)
        {
            m_DependencyAssets = dependencyAssets?.ToList();
            m_AssetComparisonResult = assetComparisonResult;
            m_ThumbnailUrl = thumbnailUrl;
            m_SourceFiles = sourceFiles?.ToList();
            m_PrimarySourceFile = primarySourceFile;
            m_UVCSFiles = uvcsFiles?.ToList();
            m_Versions = versions.ToList();
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
            bool isFrozen,
            IEnumerable<string> tags,
            IEnumerable<AssetDataFile> sourceFiles,
            IEnumerable<DependencyAsset> dependencyAssets)
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
            m_IsFrozen = isFrozen;
            m_Tags = tags.ToList();
            m_SourceFiles = sourceFiles?.Cast<IAssetDataFile>().ToList();
            m_DependencyAssets = dependencyAssets?.ToList();

            m_PrimarySourceFile = m_SourceFiles
                ?.FilterUsableFilesAsPrimaryExtensions()
                .OrderBy(x => x, new AssetDataFileComparerByExtension())
                .LastOrDefault();
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
            m_IsFrozen = other.IsFrozen;
            m_Tags = other.Tags.ToList();
        }

        public async Task GetThumbnailAsync(Action<AssetIdentifier, Texture2D> callback = null,
            CancellationToken token = default)
        {
            if (!string.IsNullOrEmpty(m_ThumbnailUrl))
            {
                var texture = ServicesContainer.instance.Resolve<IThumbnailDownloader>()
                    .GetCachedThumbnail(m_ThumbnailUrl);

                if (texture != null)
                {
                    callback?.Invoke(Identifier, texture);
                    return;
                }
            }

            await GetThumbnailUrlAsync(token);

            ServicesContainer.instance.Resolve<IThumbnailDownloader>()
                .DownloadThumbnail(Identifier, m_ThumbnailUrl, callback);
        }

        public async Task GetPreviewStatusAsync(
            Action<AssetIdentifier, IEnumerable<AssetPreview.IStatus>> callback = null,
            CancellationToken token = default)
        {
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
                else
                {
                    m_AssetComparisonResult = AssetComparisonResult.None;
                }
            }

            if (m_PreviewStatusTask != null)
            {
                m_AssetComparisonResult = await m_PreviewStatusTask;
                m_PreviewStatusTask = null;
            }

            callback?.Invoke(Identifier, PreviewStatus);
        }

        public async Task ResolvePrimaryExtensionAsync(Action<AssetIdentifier, string> callback,
            CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(m_PrimarySourceFile?.Extension))
            {
                m_PrimaryExtensionTask ??= RefreshSourceFilesAndPrimaryExtensionAsync(token);

                try
                {
                    await m_PrimaryExtensionTask;
                }
                catch (ForbiddenException)
                {
                    // Ignore if the Asset is unavailable
                }
                finally
                {
                    m_PrimaryExtensionTask = null;
                }
            }

            callback?.Invoke(Identifier, m_PrimarySourceFile?.Extension ?? NoPrimaryExtension);
        }

        public async Task SyncWithCloudAsync(Action<AssetIdentifier> callback, CancellationToken token = default)
        {
            // If there is already a sync operation in progress, wait for it to finish, but no need to trigger another sync
            if (m_SyncWithCloudLatestTask != null)
            {
                await m_SyncWithCloudLatestTask;
            }
            else
            {
                if (m_SyncWithCloudTask == null)
                {
                    CleanCachedData();

                    var tasks = new List<Task>
                    {
                        RefreshAsync(token),
                        RefreshSourceFilesAndPrimaryExtensionAsync(token),
                        GetThumbnailUrlAsync(token),
                        GetDependenciesAsync(token),
                        RefreshVersionsAsync(token)
                    };

                    m_SyncWithCloudTask = Task.WhenAll(tasks);
                }

                try
                {
                    await m_SyncWithCloudTask;
                }
                catch (OperationCanceledException)
                {
                    // Ignore if the task is manually cancelled
                }
                finally
                {
                    // If the task fails for whatever reason, at least make sure to clear it.
                    m_SyncWithCloudTask = null;
                }
            }

            callback?.Invoke(Identifier);
        }

        public async Task SyncWithCloudLatestAsync(Action<AssetIdentifier> callback, CancellationToken token = default)
        {
            // If there is already a sync operation in progress, wait for it to finish
            // There are no guarantees that the ongoing sync operation will be the latest one
            if (m_SyncWithCloudTask != null)
            {
                await m_SyncWithCloudTask;
            }

            // Use the same task to avoid multiple syncs at the same time
            if (m_SyncWithCloudLatestTask == null)
            {
                CleanCachedData();

                m_SyncWithCloudLatestTask = UpdateAssetToLatestAsync(token);
            }

            try
            {
                await m_SyncWithCloudLatestTask;
            }
            finally
            {
                // If the task fails for whatever reason, at least make sure to clear it.
                m_SyncWithCloudLatestTask = null;
            }

            callback?.Invoke(Identifier);
        }

        async Task UpdateAssetToLatestAsync(CancellationToken token)
        {
            var assetsSdkProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
            var latestAssetData = await assetsSdkProvider.GetLatestAssetVersionAsync(m_Identifier, token);

            FillFromOther(latestAssetData);

            var tasks = new List<Task>
            {
                RefreshSourceFilesAndPrimaryExtensionAsync(token),
                GetThumbnailUrlAsync(token),
                GetDependenciesAsync(token),
                RefreshVersionsAsync(token)
            };

            await Task.WhenAll(tasks);
        }

        void CleanCachedData()
        {
            m_ThumbnailUrl = null;
            m_PrimarySourceFile = null;
            m_SourceFiles.Clear();
            m_DependencyAssets.Clear();
            m_UVCSFiles.Clear();
            m_Versions.Clear();
        }

        async Task GetThumbnailUrlAsync(CancellationToken token)
        {
            var assetsSdkProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();

            m_GetPreviewStatusTask ??= assetsSdkProvider.GetPreviewUrlAsync(this, token);

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

        async Task RefreshAsync(CancellationToken token = default)
        {
            var assetsSdkProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
            var updatedAsset = await assetsSdkProvider.GetAssetAsync(Identifier, token);

            FillFromOther(updatedAsset);
        }

        public async Task RefreshSourceFilesAndPrimaryExtensionAsync(CancellationToken token = default)
        {
            m_RefreshFilesTask ??= RefreshSourceFilesAndPrimaryExtensionInternalAsync(token);
            try
            {
                await m_RefreshFilesTask;
            }
            catch (HttpRequestException)
            {
                // Ignore unreachable host
            }
            finally
            {
                m_RefreshFilesTask = null;
            }
        }

        async Task RefreshSourceFilesAndPrimaryExtensionInternalAsync(CancellationToken token)
        {
            var files = new List<IAssetDataFile>();
            var extensions = new HashSet<string>();

            var assetsSdkProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();

            await foreach (var file in assetsSdkProvider.ListFilesAsync(this, Range.All, token))
            {
                files.Add(file);
                extensions.Add(Path.GetExtension(file.Path));
            }

            m_SourceFiles = files;
            m_PrimarySourceFile = m_SourceFiles
                .FilterUsableFilesAsPrimaryExtensions()
                .OrderBy(x => x, new AssetDataFileComparerByExtension())
                .LastOrDefault();
        }

        async Task GetDependenciesAsync(CancellationToken token)
        {
            var deps = new List<DependencyAsset>();
            await foreach (var dependency in AssetDataDependencyHelper.LoadDependenciesAsync(this, token))
            {
                deps.Add(new DependencyAsset(dependency.Identifier, dependency.AssetData));
            }

            m_DependencyAssets = deps;
        }

        public async Task RefreshVersionsAsync(CancellationToken token = default)
        {
            m_Versions.Clear();

            var assetsSdkProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
            var versions = new List<IAssetData>();
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
