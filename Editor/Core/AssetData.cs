using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal interface IAssetData
    {
        string name { get; }
        AssetIdentifier identifier { get; }
        AssetType assetType { get; }
        string status { get; }
        string previewFilePath { get; }
        string previewFileUrl { get; }
        DateTime updated { get; }
        DateTime created { get; }
        IEnumerable<string> tags { get; }
        string description { get; }
        string authorName { get; }
        string defaultImportPath { get; }
        Task<string> GetPrimaryExtension();
        Task GetPrimaryExtension(Action<AssetIdentifier, string> callback);
        IEnumerable<IAssetDataFile> cachedFiles { get; }
        IAsyncEnumerable<IFile> GetAllCloudFilesAsync(CancellationToken token = default);
        IAsyncEnumerable<IFile> GetSourceCloudFilesAsync(CancellationToken token = default);
        Task SyncWithCloudAsync(CancellationToken token = default);
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
        public DateTime created => Asset.AuthoringInfo?.Created ?? DateTime.MinValue;
        public DateTime updated => Asset.AuthoringInfo?.Updated ?? DateTime.MinValue;
        public string authorName => Asset.AuthoringInfo?.CreatedBy ?? "<None>";
        public IEnumerable<string> tags => Asset.Tags;
        public string previewFilePath => Asset.PreviewFile;
        public string previewFileUrl => Asset.PreviewFileUrl?.ToString() ?? string.Empty;

        [SerializeField]
        string m_PrimaryExtension;
 
        [SerializeReference]
        List<IAssetDataFile> m_CachedFiles = new ();
      
        [SerializeField]
        string m_JsonAssetSerialized;
        
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
        
        public async Task<string> GetPrimaryExtension()
        {
            if (!string.IsNullOrEmpty(m_PrimaryExtension))
            {
                return m_PrimaryExtension;
            }

            return m_PrimaryExtension = await AssetDataTypeHelper.GetAssetPrimaryExtension(this) ?? NoPrimaryExtension;
        }

        public async Task GetPrimaryExtension(Action<AssetIdentifier, string> callback)
        {
            var extension = await GetPrimaryExtension();
            callback.Invoke(identifier, extension);
        }

        public async IAsyncEnumerable<IFile> GetAllCloudFilesAsync([EnumeratorCancellation] CancellationToken token)
        {
            await foreach (var file in m_Asset.ListFilesAsync(Range.All, token))
            {
                yield return file;
            }
        }

        public IEnumerable<IAssetDataFile> cachedFiles => m_CachedFiles;

        public async IAsyncEnumerable<IFile> GetSourceCloudFilesAsync([EnumeratorCancellation] CancellationToken token)
        {
            m_CachedFiles.Clear();
            
            await foreach(var dataset in m_Asset.ListDatasetsAsync(Range.All, token))
            {
                if (!dataset.Name.Contains(k_SourceDataSetName)) continue;
                
                await foreach(var file in dataset.ListFilesAsync(Range.All, token))
                {
                    m_CachedFiles.Add(new AssetDataFile(file));
                    yield return file;
                }
                
                yield break;
            }
        }
        
        public async Task SyncWithCloudAsync(CancellationToken token = default)
        {
            await m_Asset.RefreshAsync(new FieldsFilter { AssetFields = AssetFields.authoring }, token);
            await foreach(var _ in GetSourceCloudFilesAsync(token)) { };
            await GetPrimaryExtension();
        }

        static AssetDescriptor BuildDescriptor(OrganizationId organizationId, ProjectId projectId, AssetId assetId, AssetVersion assetVersionId)
        {
            var projectDescriptor = new ProjectDescriptor(organizationId, projectId);
            return new AssetDescriptor(projectDescriptor, assetId, assetVersionId);
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

        public static bool IsDifferent(AssetData assetData1, AssetData assetData2)
        {
            if (assetData1?.m_Asset == null || assetData2?.m_Asset == null)
                return false;

            return assetData1.m_Asset.Descriptor != assetData2.m_Asset.Descriptor;
        }
    }
}
