using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IAssetData
    {
        string name { get; }
        AssetIdentifier identifier { get; }
        AssetType assetType { get; }
        string status { get; }
        DateTime? updated { get; }
        DateTime? created { get; }
        IEnumerable<string> tags { get; }
        string description { get; }
        string authorName { get; }
        string primaryExtension { get; }
        IEnumerable<IAssetDataFile> sourceFiles { get; }
        IEnumerable<AssetPreview.IStatus> previewStatus { get; }
        IEnumerable<DependencyAsset> dependencies { get; }

        Task GetThumbnailAsync(Action<AssetIdentifier, Texture2D> callback = null, CancellationToken token = default);
        Task ResolvePrimaryExtensionAsync(Action<AssetIdentifier, string> callback, CancellationToken token = default);
        Task SyncWithCloudAsync(Action<AssetIdentifier> callback, CancellationToken token = default);

        Task GetPreviewStatusAsync(Action<AssetIdentifier, IEnumerable<AssetPreview.IStatus>> callback = null, CancellationToken token = default); // TODO Move away all methods not related to the actual IAssetData raw data
        IAsyncEnumerable<IFile> GetSourceCloudFilesAsync(CancellationToken token = default); // TODO Move away all methods not related to the actual IAssetData raw data
    }

    static class AssetDataExtension
    {
        public static bool IsTheSame(this IAssetData assetData, IAssetData other)
        {
            if (ReferenceEquals(null, other))
                return false;

            if (ReferenceEquals(assetData, other))
                return true;

            if (other.GetType() != assetData.GetType())
                return false;

            return assetData.name == other.name
                   && assetData.identifier.Equals(other.identifier)
                   && assetData.assetType == other.assetType
                   && assetData.status == other.status
                   && assetData.updated == other.updated
                   && assetData.created == other.created
                   && assetData.tags.SequenceEqual(other.tags)
                   && assetData.description == other.description
                   && assetData.authorName == other.authorName
                   && assetData.primaryExtension == other.primaryExtension
                   && assetData.sourceFiles.SequenceEqual(other.sourceFiles);
        }
    }

    [Serializable]
    internal class AssetData : IAssetData, ISerializationCallbackReceiver
    {
        public AssetIdentifier identifier => m_Identifier ??= new AssetIdentifier(Asset.Descriptor);
        public string name => Asset.Name;
        public AssetType assetType => Asset.Type.ConvertCloudAssetTypeToAssetType();
        public string status => Asset.Status;
        public string description => Asset.Description;
        public DateTime? created => Asset.AuthoringInfo?.Created;
        public DateTime? updated => Asset.AuthoringInfo?.Updated;
        public string authorName => Asset.AuthoringInfo?.CreatedBy.ToString() ?? null;
        public IEnumerable<string> tags => Asset.Tags;

        [SerializeField]
        string m_PrimaryExtension;

        public string primaryExtension => m_PrimaryExtension;

        [SerializeReference]
        List<IAssetDataFile> m_SourceFiles = new();

        public IEnumerable<IAssetDataFile> sourceFiles => m_SourceFiles;

        [SerializeField]
        List<DependencyAsset> m_DependencyAssets = new();

        public IEnumerable<DependencyAsset> dependencies => m_DependencyAssets;

        [SerializeField]
        string m_JsonAssetSerialized;

        [SerializeField]
        AssetComparisonResult m_AssetComparisonResult = AssetComparisonResult.None;

        [SerializeField]
        string m_ThumbnailUrl;

        Task m_PrimaryExtensionTask;
        Task<AssetComparisonResult> m_PreviewStatusTask;
        Task m_SyncWithCloudTask;
        Task m_RefreshFilesTask;
        Task<Uri> m_GetPreviewStatusTask;

        public IEnumerable<AssetPreview.IStatus> previewStatus
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

        IAsset m_Asset;
        IAsset Asset => m_Asset ??= Services.AssetRepository.DeserializeAsset(m_JsonAssetSerialized);

        public static readonly string NoPrimaryExtension = "unknown";

        AssetIdentifier m_Identifier;

        public AssetData(IAsset cloudAsset)
        {
            m_Asset = cloudAsset;
        }

        ~AssetData()
        {
            OnBeforeSerialize();
        }

        void CleanCachedData()
        {
            m_PrimaryExtension = null;
            m_ThumbnailUrl = null;
            m_SourceFiles.Clear();
            m_DependencyAssets.Clear();
        }

        public async Task GetThumbnailAsync(Action<AssetIdentifier, Texture2D> callback = null, CancellationToken token = default)
        {
            if (!string.IsNullOrEmpty(m_ThumbnailUrl))
            {
                var texture = ServicesContainer.instance.Resolve<IThumbnailDownloader>().GetCachedThumbnail(m_ThumbnailUrl);

                if (texture != null)
                {
                    callback?.Invoke(identifier, texture);
                    return;
                }
            }

            await GetThumbnailUrlAsync(token);

            ServicesContainer.instance.Resolve<IThumbnailDownloader>().DownloadThumbnail(identifier, m_ThumbnailUrl, callback);
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
            finally
            {
                m_GetPreviewStatusTask = null;
            }

            m_ThumbnailUrl = previewFileUrl?.ToString() ?? string.Empty;
        }

        public async Task GetPreviewStatusAsync(Action<AssetIdentifier, IEnumerable<AssetPreview.IStatus>> callback = null, CancellationToken token = default)
        {
            if (m_PreviewStatusTask == null)
            {
                var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
                if (assetDataManager.IsInProject(identifier))
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

            callback?.Invoke(identifier, previewStatus);
        }

        public async Task ResolvePrimaryExtensionAsync(Action<AssetIdentifier, string> callback, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(m_PrimaryExtension))
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

            callback?.Invoke(identifier, m_PrimaryExtension);
        }

        public async IAsyncEnumerable<IFile> GetSourceCloudFilesAsync([EnumeratorCancellation] CancellationToken token = default)
        {
            var sourceDataset = await Asset.GetSourceDatasetAsync(token);

            if (sourceDataset == null)
                yield break;

            await foreach (var file in sourceDataset.ListFilesAsync(Range.All, token))
            {
                yield return file;
            }
        }

        public async Task RefreshSourceFilesAndPrimaryExtensionAsync(CancellationToken token = default)
        {
            m_RefreshFilesTask ??= RefreshSourceFilesAndPrimaryExtensionInternalAsync(token);

            await m_RefreshFilesTask;
            m_RefreshFilesTask = null;
        }

        async Task RefreshSourceFilesAndPrimaryExtensionInternalAsync(CancellationToken token)
        {
            var files = new List<IAssetDataFile>();

            var extensions = new List<string>();
            await foreach (var file in GetSourceCloudFilesAsync(token))
            {
                files.Add(new AssetDataFile(file));
                extensions.Add(Path.GetExtension(file.Descriptor.Path));
            }

            m_SourceFiles = files;
            m_PrimaryExtension = AssetDataTypeHelper.GetAssetPrimaryExtension(extensions) ?? NoPrimaryExtension;
        }

        public async Task SyncWithCloudAsync(Action<AssetIdentifier> callback, CancellationToken token = default)
        {
            if (m_SyncWithCloudTask == null)
            {
                CleanCachedData();

                var tasks = new List<Task>
                {
                    Asset.RefreshAsync(token),
                    RefreshSourceFilesAndPrimaryExtensionAsync(token),
                    GetThumbnailUrlAsync(token),
                    GetDependenciesAsync(token)
                };

                m_SyncWithCloudTask = Task.WhenAll(tasks);
            }

            await m_SyncWithCloudTask;
            m_SyncWithCloudTask = null;

            callback?.Invoke(identifier);
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
    }
}
