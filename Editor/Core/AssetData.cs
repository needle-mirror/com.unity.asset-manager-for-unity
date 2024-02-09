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
        AssetIdentifier id { get; }
        AssetType assetType { get; }
        string status { get; }
        string projectId { get; }
        string organizationId { get; }
        string assetId { get; }
        string versionId { get; }
        IAsyncEnumerable<IFile> GetFilesAsync(CancellationToken token = default);
        string previewFilePath { get; }
        string previewFileUrl { get; }
        DateTime updated { get; }
        DateTime created { get; }
        IEnumerable<string> tags { get; }
        string description { get; }
        string authorName { get; }
        string defaultImportPath { get; }
        Task<IAsset> GetDetailedAssetAsync(CancellationToken token = default);
    }

    [Serializable]
    internal class AssetData : IAssetData, ISerializationCallbackReceiver
    {
        public string defaultImportPath => Path.Combine(Constants.AssetsFolderName, Constants.ApplicationFolderName, $"{Regex.Replace(name, @"[\\\/:*?""<>|]", "").Trim()}");

        public string name => Asset.Name;
        public string organizationId => Asset.Descriptor.OrganizationGenesisId.ToString();
        public string projectId => Asset.Descriptor.ProjectId.ToString();
        public string assetId => Asset.Descriptor.AssetId.ToString();
        public string versionId => Asset.Descriptor.AssetVersion.ToString();
        public AssetType assetType => Asset.Type.ConvertCloudAssetTypeToAssetType();
        public string status => Asset.Status;
        public string description => Asset.Description;
        public DateTime created => Asset.AuthoringInfo?.Created ?? DateTime.MinValue;
        public DateTime updated => Asset.AuthoringInfo?.Updated ?? DateTime.MinValue;
        public string authorName => Asset.AuthoringInfo?.CreatedBy ?? "<None>";
        public IEnumerable<string> tags => Asset.Tags;
        public string previewFilePath => Asset.PreviewFile;
        public string previewFileUrl => Asset.PreviewFileUrl?.ToString() ?? string.Empty;
        
        AssetIdentifier m_Identifier;
        public AssetIdentifier id => // TODO Use AssetDescriptor?
            m_Identifier ??= new AssetIdentifier
            {
                sourceId = assetId,
                organizationId = organizationId,
                projectId = projectId,
                version = versionId,
            };

        const string k_SourceDataSetName = "Source"; // Temporary solution until we have a better way to identify the source dataset
        
        public async IAsyncEnumerable<IFile> GetFilesAsync([EnumeratorCancellation] CancellationToken token)
        {
            await foreach(var dataset in m_Asset.ListDatasetsAsync(Range.All, token))
            {
                if (!dataset.Name.Contains(k_SourceDataSetName)) continue;
                
                await foreach(var file in dataset.ListFilesAsync(Range.All, token))
                {
                    yield return file;
                }
                
                yield break;
            }
        }
        
        public async Task<IAsset> GetDetailedAssetAsync(CancellationToken token = default)
        {
            return await Services.AssetRepository.GetAssetAsync(m_Asset.Descriptor, new FieldsFilter{ AssetFields = AssetFields.authoring }, token); // TODO Get the description
        }

        IAsset m_Asset;
        IAsset Asset => m_Asset ??= Services.AssetRepository.DeserializeAsset(m_JsonAssetSerialized);
        
        [SerializeField]
        string m_JsonAssetSerialized;

        public AssetData(IAsset cloudAsset)
        {
            m_Asset = cloudAsset;
        }
        
        ~AssetData()
        {
            OnBeforeSerialize();
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
