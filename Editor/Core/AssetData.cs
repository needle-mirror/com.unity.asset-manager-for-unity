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
        string Name { get; }
        AssetIdentifier Identifier { get; }
        AssetType AssetType { get; }
        string Status { get; }
        DateTime? Updated { get; }
        DateTime? Created { get; }
        IEnumerable<string> Tags { get; }
        string Description { get; }
        string CreatedBy { get; }
        string UpdatedBy { get; }
        string PrimaryExtension { get; }
        IEnumerable<IAssetDataFile> SourceFiles { get; }
        IEnumerable<AssetPreview.IStatus> PreviewStatus { get; }
        IEnumerable<DependencyAsset> Dependencies { get; }

        Task GetThumbnailAsync(Action<AssetIdentifier, Texture2D> callback = null, CancellationToken token = default);
        Task ResolvePrimaryExtensionAsync(Action<AssetIdentifier, string> callback, CancellationToken token = default);
        Task SyncWithCloudAsync(Action<AssetIdentifier> callback, CancellationToken token = default);

        Task GetPreviewStatusAsync(Action<AssetIdentifier, IEnumerable<AssetPreview.IStatus>> callback = null,
            CancellationToken token =
                default); // TODO Move away all methods not related to the actual IAssetData raw data

        IAsyncEnumerable<IFile>
            GetSourceCloudFilesAsync(
                CancellationToken token =
                    default); // TODO Move away all methods not related to the actual IAssetData raw data
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
                && assetData.PrimaryExtension == other.PrimaryExtension
                && assetData.SourceFiles.SequenceEqual(other.SourceFiles);
        }
    }

    [Serializable]
    class AssetData : IAssetData, ISerializationCallbackReceiver
    {
        public static readonly string NoPrimaryExtension = "unknown";

        [SerializeField]
        string m_PrimaryExtension;

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

        IAsset m_Asset;
        AssetIdentifier m_Identifier;

        Task<Uri> m_GetPreviewStatusTask;
        Task<AssetComparisonResult> m_PreviewStatusTask;
        Task m_PrimaryExtensionTask;
        Task m_RefreshFilesTask;
        Task m_SyncWithCloudTask;

        public IAsset Asset => m_Asset ??= Services.AssetRepository.DeserializeAsset(m_JsonAssetSerialized);
        public AssetIdentifier Identifier => m_Identifier ??= new AssetIdentifier(Asset.Descriptor);
        public string Name => Asset.Name;
        public AssetType AssetType => Asset.Type.ConvertCloudAssetTypeToAssetType();
        public string Status => Asset.Status;
        public string Description => Asset.Description;
        public DateTime? Created => Asset.AuthoringInfo?.Created;
        public DateTime? Updated => Asset.AuthoringInfo?.Updated;
        public string CreatedBy => Asset.AuthoringInfo?.CreatedBy.ToString() ?? null;
        public string UpdatedBy => Asset.AuthoringInfo?.UpdatedBy.ToString() ?? null;
        public IEnumerable<string> Tags => Asset.Tags;
        public string PrimaryExtension => m_PrimaryExtension;
        public IEnumerable<IAssetDataFile> SourceFiles => m_SourceFiles;
        public IEnumerable<DependencyAsset> Dependencies => m_DependencyAssets;
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
                if (assetDataManager.IsInProject(Identifier))
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

            callback?.Invoke(Identifier, m_PrimaryExtension);
        }

        public async IAsyncEnumerable<IFile> GetSourceCloudFilesAsync(
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

            callback?.Invoke(Identifier);
        }

        public void OnBeforeSerialize()
        {
            if (m_Asset != null)
            {
                m_JsonAssetSerialized = m_Asset.Serialize();
            }
        }

        public void OnAfterDeserialize() { }

        void CleanCachedData()
        {
            m_PrimaryExtension = null;
            m_ThumbnailUrl = null;
            m_SourceFiles.Clear();
            m_DependencyAssets.Clear();
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

        async Task GetDependenciesAsync(CancellationToken token)
        {
            var deps = new List<DependencyAsset>();
            await foreach (var dep in AssetDataDependencyHelper.LoadDependenciesAsync(this, false, token))
            {
                deps.Add(new DependencyAsset(dep.Identifier, dep.AssetData));
            }

            m_DependencyAssets = deps;
        }
    }
}
