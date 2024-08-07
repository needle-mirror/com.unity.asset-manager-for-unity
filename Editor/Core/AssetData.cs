using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;
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
    class AssetData : IAssetData, ISerializationCallbackReceiver
    {
        public static readonly string NoPrimaryExtension = "unknown";
        static readonly string k_UVCSTag = "SourceControl";

        [SerializeField] 
        List<DependencyAsset> m_DependencyAssets = new();

        [SerializeField] 
        string m_JsonAssetSerialized;

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

        IAsset m_Asset;
        AssetIdentifier m_Identifier;

        Task<Uri> m_GetPreviewStatusTask;
        Task<AssetComparisonResult> m_PreviewStatusTask;
        Task m_PrimaryExtensionTask;
        Task m_RefreshFilesTask;
        Task m_RefreshUVCSFilesTask;
        Task m_SyncWithCloudTask;
        Task m_SyncWithCloudLatestTask;

        public IAsset Asset
        {
            get
            {
                if (m_Asset == null)
                {
                    // Internal Asset is null. Probably because of a domain reload. Ask the assets provider to deserialized
                    // our internal data.
                    var assetsProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
                    assetsProvider.OnAfterDeserializeAssetData(this);
                }

                return m_Asset;
            }
            // Only AssetsSdkProvider should be setting this value
            set { m_Asset = value; }
        }

        // Only AssetsSdkProvider should be getting this value
        public string AssetSerialized => m_JsonAssetSerialized;

        public AssetIdentifier Identifier => m_Identifier ??= Map(Asset.Descriptor);
        public int SequenceNumber => Asset.FrozenSequenceNumber;
        public int ParentSequenceNumber => Asset.ParentFrozenSequenceNumber;
        public string Changelog => Asset.Changelog;
        public string Name => Asset.Name;
        public AssetType AssetType => Map(Asset.Type);
        public string Status => Asset.Status;
        public string Description => Asset.Description;
        public DateTime? Created => Asset.AuthoringInfo?.Created;
        public DateTime? Updated => Asset.AuthoringInfo?.Updated;
        public string CreatedBy => Asset.AuthoringInfo?.CreatedBy.ToString() ?? null;
        public string UpdatedBy => Asset.AuthoringInfo?.UpdatedBy.ToString() ?? null;
        public bool IsFrozen => Asset.IsFrozen;
        public IEnumerable<string> Tags => Asset.Tags;
        public IAssetDataFile PrimarySourceFile => m_PrimarySourceFile;
        public IEnumerable<IAssetDataFile> SourceFiles => m_SourceFiles;
        public IEnumerable<DependencyAsset> Dependencies => m_DependencyAssets;
        public IEnumerable<IAssetDataFile> UVCSFiles => m_UVCSFiles;

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

        public AssetData(IAsset cloudAsset)
        {
            m_Asset = cloudAsset;
        }

        ~AssetData()
        {
            OnBeforeSerialize();
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
                    if (string.IsNullOrEmpty(m_PrimarySourceFile?.Extension))
                    {
                        m_PrimaryExtensionTask ??= RefreshUVCSFilesAndPrimaryExtensionAsync(token);
                        await m_PrimaryExtensionTask;
                    }
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
                        Asset.RefreshAsync(token),
                        RefreshSourceFilesAndPrimaryExtensionAsync(token),
                        RefreshUVCSFilesAndPrimaryExtensionAsync(token),
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
            try
            {
                m_Asset = await Asset.WithLatestVersionAsync(token);
            }
            catch (NotFoundException)
            {
                await using var enumerator = GetAssetsInDescendingVersionNumberOrder(token).GetAsyncEnumerator(token);
                m_Asset = await enumerator.MoveNextAsync() ? enumerator.Current : default;
            }
            
            m_Identifier = Map(m_Asset.Descriptor);

            var tasks = new List<Task>
            {
                RefreshSourceFilesAndPrimaryExtensionAsync(token),
                RefreshUVCSFilesAndPrimaryExtensionAsync(token),
                GetThumbnailUrlAsync(token),
                GetDependenciesAsync(token),
                RefreshVersionsAsync(token)
            };

            await Task.WhenAll(tasks);
        }

        public void OnBeforeSerialize()
        {
            if (m_Asset != null)
            {
                m_JsonAssetSerialized = m_Asset.Serialize();
            }
        }

        public void OnAfterDeserialize()
        {
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
            m_GetPreviewStatusTask ??= Asset.GetPreviewUrlAsync(token);

            Uri previewFileUrl = null;

            try
            {
                previewFileUrl = await m_GetPreviewStatusTask;
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
            await foreach (var file in GetSourceCloudFilesAsync(token))
            {
                files.Add(new AssetDataFile(file));
                extensions.Add(Path.GetExtension(file.Descriptor.Path));
            }

            m_SourceFiles = files;
            m_PrimarySourceFile = m_SourceFiles
                .FilterUsableFilesAsPrimaryExtensions()
                .OrderBy(x => x, new AssetDataFileComparerByExtension())
                .LastOrDefault();
        }

        async IAsyncEnumerable<IFile> GetSourceCloudFilesAsync(
            [EnumeratorCancellation] CancellationToken token = default)
        {
            var sourceDataset = await Asset.GetSourceDatasetAsync(token);

            if (sourceDataset == null)
            {
                yield break;
            }

            await foreach (var file in sourceDataset.ListFilesAsync(Range.All, token))
            {
                yield return file;
            }
        }

        async Task RefreshUVCSFilesAndPrimaryExtensionAsync(CancellationToken token = default)
        {
            m_RefreshUVCSFilesTask ??= RefreshUVCSFilesAndPrimaryExtensionInternalAsync(token);

            await m_RefreshUVCSFilesTask;
            m_RefreshUVCSFilesTask = null;
        }

        async Task RefreshUVCSFilesAndPrimaryExtensionInternalAsync(CancellationToken token)
        {
            var files = new List<IAssetDataFile>();

            var extensions = new List<string>();
            await foreach (var file in GetUVCSCloudFilesAsync(token))
            {
                files.Add(new AssetDataFile(file));
                extensions.Add(Path.GetExtension(file.Descriptor.Path));
            }

            m_UVCSFiles = files;
            m_PrimarySourceFile = m_UVCSFiles
                .FilterUsableFilesAsPrimaryExtensions()
                .OrderBy(x => x, new AssetDataFileComparerByExtension())
                .LastOrDefault();
        }

        async IAsyncEnumerable<IFile> GetUVCSCloudFilesAsync(
            [EnumeratorCancellation] CancellationToken token = default)
        {
            IDataset uvcsDataset = null;
            await foreach (var dataset in Asset.ListDatasetsAsync(Range.All, token))
            {
                if (dataset.SystemTags != null && dataset.SystemTags.Contains(k_UVCSTag))
                {
                    uvcsDataset = dataset;
                }
            }

            if (uvcsDataset == null)
            {
                yield break;
            }

            await foreach (var file in uvcsDataset.ListFilesAsync(Range.All, token))
            {
                yield return file;
            }
        }

        async Task GetDependenciesAsync(CancellationToken token)
        {
            var deps = new List<DependencyAsset>();
            await foreach (var dep in AssetDataDependencyHelper.LoadDependenciesAsync(this, false, token))
            {
                deps.Add(new DependencyAsset(dep.Identifier, dep.AssetData));
            }

            m_DependencyAssets = deps;
        }

        public async Task RefreshVersionsAsync(CancellationToken token = default)
        {
            m_Versions.Clear();

            var versions = new List<IAssetData>();
            try
            {
                await foreach (var assetData in GetAssetDataInDescendingVersionNumberOrder(token))
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

        public async IAsyncEnumerable<AssetData> GetAssetDataInDescendingVersionNumberOrder(
            [EnumeratorCancellation] CancellationToken token = default)
        {
            await foreach (var version in GetAssetsInDescendingVersionNumberOrder(token))
            {
                yield return version == null ? null : new AssetData(version);
            }
        }
        
        async IAsyncEnumerable<IAsset> GetAssetsInDescendingVersionNumberOrder(
            [EnumeratorCancellation] CancellationToken token = default)
        {
            await foreach (var version in Asset.QueryVersions()
                               .OrderBy("versionNumber", SortingOrder.Descending)
                               .ExecuteAsync(token))
            {
                yield return version;
            }
        }

        static AssetType Map(Unity.Cloud.Assets.AssetType assetType)
        {
            return assetType switch
            {
                Cloud.Assets.AssetType.Asset_2D => AssetType.Asset2D,
                Cloud.Assets.AssetType.Model_3D => AssetType.Model3D,
                Cloud.Assets.AssetType.Audio => AssetType.Audio,
                Cloud.Assets.AssetType.Material => AssetType.Material,
                Cloud.Assets.AssetType.Script => AssetType.Script,
                Cloud.Assets.AssetType.Video => AssetType.Video,
                _ => AssetType.Other
            };
        }

        static AssetIdentifier Map(AssetDescriptor descriptor)
        {
            return new AssetIdentifier(descriptor.OrganizationId.ToString(),
                descriptor.ProjectId.ToString(),
                descriptor.AssetId.ToString(),
                descriptor.AssetVersion.ToString());
        }
    }
}