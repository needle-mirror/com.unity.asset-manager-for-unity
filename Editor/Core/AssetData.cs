using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
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
        string defaultImportPath { get; }
        string primaryExtension { get; }
        IEnumerable<IAssetDataFile> sourceFiles { get; }
        AssetPreview.IStatus previewStatus { get; }

        Task GetThumbnailAsync(Action<AssetIdentifier, Texture2D> callback = null);
        Task GetPreviewStatusAsync(Action<AssetIdentifier, AssetPreview.IStatus> callback = null); // TODO Move away all methods not related to the actual IAssetData raw data
        Task ResolvePrimaryExtensionAsync(Action<AssetIdentifier, string> callback);
        IAsyncEnumerable<IFile> GetSourceCloudFilesAsync(CancellationToken token = default);
        Task SyncWithCloudAsync(Action<AssetIdentifier> callback, CancellationToken token = default);
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
                   && assetData.defaultImportPath == other.defaultImportPath
                   && assetData.primaryExtension == other.primaryExtension
                   && assetData.sourceFiles.SequenceEqual(other.sourceFiles)
                   && assetData.previewStatus == other.previewStatus;
        }
    }

    [Serializable]
    internal class AssetData : IAssetData, ISerializationCallbackReceiver
    {
        public string defaultImportPath => Path.Combine(Constants.AssetsFolderName, Constants.ApplicationFolderName, $"{Regex.Replace(name, @"[\\\/:*?""<>|]", "").Trim()}");

        public AssetIdentifier identifier => m_Identifier ??= new AssetIdentifier(Asset.Descriptor);
        public string name => Asset.Name;
        public AssetType assetType => Asset.Type.ConvertCloudAssetTypeToAssetType();
        public string status => Asset.Status;
        public string description => Asset.Description;
        public DateTime? created => Asset.AuthoringInfo?.Created;
        public DateTime? updated => Asset.AuthoringInfo?.Updated;
        public string authorName => Asset.AuthoringInfo?.CreatedBy ?? null;
        public IEnumerable<string> tags => Asset.Tags;

        [SerializeField]
        string m_PrimaryExtension;

        public string primaryExtension => m_PrimaryExtension;

        [SerializeReference]
        List<IAssetDataFile> m_SourceFiles = new();

        public IEnumerable<IAssetDataFile> sourceFiles => m_SourceFiles;

        [SerializeField]
        string m_JsonAssetSerialized;

        [SerializeField]
        AssetComparisonResult m_AssetComparisonResult = AssetComparisonResult.None;

        public AssetPreview.IStatus previewStatus
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

                return s;
            }
        }

        IAsset m_Asset;
        IAsset Asset => m_Asset ??= Services.AssetRepository.DeserializeAsset(m_JsonAssetSerialized);

        public static readonly string NoPrimaryExtension = "unknown";

        AssetIdentifier m_Identifier;

        const string k_SourceDataSetName = "Source"; // Temporary solution until we have a better way to identify the source dataset

        public AssetData(IAsset cloudAsset)
        {
            m_Asset = cloudAsset;
        }

        ~AssetData()
        {
            OnBeforeSerialize();
        }

        public Task GetThumbnailAsync(Action<AssetIdentifier, Texture2D> callback = null)
        {
            var previewFileUrl = Asset.PreviewFileUrl?.ToString() ?? string.Empty;
            ServicesContainer.instance.Resolve<IThumbnailDownloader>().DownloadThumbnail(identifier, previewFileUrl, callback);
            return Task.CompletedTask;
        }

        public async Task GetPreviewStatusAsync(Action<AssetIdentifier, AssetPreview.IStatus> callback = null)
        {
            var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
            if (assetDataManager.IsInProject(identifier))
            {
                var result = await ServicesContainer.instance.Resolve<IAssetsProvider>().CompareAssetWithCloudAsync(this);
                m_AssetComparisonResult = result;
            }

            callback?.Invoke(identifier, previewStatus); // TODO Use static instances?
        }

        public async Task<string> GetPrimaryExtension()
        {
            if (!string.IsNullOrEmpty(m_PrimaryExtension))
            {
                return m_PrimaryExtension;
            }

            // Actually we should only look at the Source dataset but for performance reason we rely on the cached files inside the Asset
            var extensions = new List<string>();
            await foreach (var f in m_Asset.ListFilesAsync(Range.All, CancellationToken.None))
            {
                extensions.Add(Path.GetExtension(f.Descriptor.Path));
            }

            return m_PrimaryExtension = AssetDataTypeHelper.GetAssetPrimaryExtension(extensions) ?? NoPrimaryExtension;
        }

        public async Task ResolvePrimaryExtensionAsync(Action<AssetIdentifier, string> callback)
        {
            var extension = await GetPrimaryExtension();
            callback?.Invoke(identifier, extension);
        }

        public async Task CompareWithCloudAsync(Action<AssetIdentifier, AssetComparisonResult> callback, CancellationToken token = default)
        {
            var result = await ServicesContainer.instance.Resolve<IAssetsProvider>().CompareAssetWithCloudAsync(this);
            m_AssetComparisonResult = result;
            callback?.Invoke(identifier, result);
        }

        public async IAsyncEnumerable<IFile> GetSourceCloudFilesAsync([EnumeratorCancellation] CancellationToken token = default)
        {
            IDataset sourceDataset = null;

            await foreach(var dataset in m_Asset.ListDatasetsAsync(Range.All, token))
            {
                if (!dataset.Name.Contains(k_SourceDataSetName))
                    continue;

                sourceDataset = dataset;
                break;
            }

            if (sourceDataset == null)
                yield break;

            // Note. dataset.ListFilesAsync generates downloadURLs and is very slow compared m_Asset.ListFilesAsync
            await m_Asset.RefreshAsync(new FieldsFilter { AssetFields = AssetFields.files, FileFields = FileFields.fileSize }, token);
            await foreach (var file in m_Asset.ListFilesAsync(Range.All, token))
            {
                if (file.Descriptor.DatasetId == sourceDataset.Descriptor.DatasetId)
                {
                    yield return file;
                }
            }
        }

        async Task RefreshSourceFilesFromCloudAsync(CancellationToken token = default)
        {
            m_SourceFiles.Clear();

            await foreach (var file in GetSourceCloudFilesAsync(token))
            {
                m_SourceFiles.Add(new AssetDataFile(file));
            }
        }

        public async Task SyncWithCloudAsync(Action<AssetIdentifier> callback, CancellationToken token = default)
        {
            var tasks = new List<Task>
            {
                m_Asset.RefreshAsync(new FieldsFilter { AssetFields = AssetFields.authoring }, token),
                RefreshSourceFilesFromCloudAsync(token),
                GetPrimaryExtension()
            };

            await Task.WhenAll(tasks);

            callback?.Invoke(identifier);
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
