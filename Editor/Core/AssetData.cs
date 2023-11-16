using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        IReadOnlyCollection<IAssetDataFile> files { get; }
        string previewFilePath { get; }
        string previewFileUrl { get; }
        DateTime updated { get; }
        DateTime created { get; }
        IReadOnlyCollection<string> tags { get; }
        string description { get; }
        string authorName { get; }
        string importPath { get; }
        AssetDataFilesStatus filesInfosStatus { get; }
    }

    [Serializable]
    internal class AssetData : IAssetData, ISerializationCallbackReceiver
    {
        public string importPath => Path.Combine(Constants.AssetsFolderName, Constants.ApplicationFolderName, Constants.ImportedFolderName, $"{name?.Replace(" ", "_").Trim() ?? string.Empty}_{id}");

        [SerializeField]
        private string m_Name;
        public string name => m_Name;
        
        [SerializeField]
        private string m_OrganizationId;
        public string organizationId => m_OrganizationId;
        
        [SerializeField]
        private string m_ProjectId;
        public string projectId => m_ProjectId;
        
        [SerializeField]
        private string m_AssetId;
        public string assetId => m_AssetId;
        
        [SerializeField]
        private string m_VersionId;
        public string versionId => m_VersionId;
        
        private AssetIdentifier m_Identifier;
        public AssetIdentifier id =>
            m_Identifier ??= new AssetIdentifier
            {
                sourceId = assetId,
                organizationId = organizationId,
                projectId = projectId,
                version = versionId,
            };

        [SerializeField]
        private AssetType m_AssetType;
        public AssetType assetType => m_AssetType;

        [SerializeField]
        private string m_Status;
        public string status => m_Status;
        
        [SerializeField]
        private string m_PreviewFilePath;
        public string previewFilePath => m_PreviewFilePath;

        [SerializeField]
        private string m_PreviewFileUrl;
        public string previewFileUrl=> m_PreviewFileUrl;

        private DateTime m_Created;
        public DateTime created => m_Created;

        private DateTime m_Updated;
        public DateTime updated => m_Updated; 
        
        [SerializeField]
        private long m_UpdatedDateTimeAsTicks;
        [SerializeField]
        private long m_CreatedDateTimeAsTicks;
        
        [SerializeField]
        private List<string> m_Tags;
        public IReadOnlyCollection<string> tags =>  m_Tags;

        [SerializeField]
        private string m_Description;
        public string description => m_Description;

        [SerializeField]
        private string m_AuthorName;
        public string authorName => m_AuthorName;

        [SerializeReference]
        private List<IAssetDataFile> m_Files;
        public IReadOnlyCollection<IAssetDataFile> files => m_Files;
        
        [SerializeField]
        private AssetDataFilesStatus m_FilesInfosStatus;
        public AssetDataFilesStatus filesInfosStatus
        {
            get => m_FilesInfosStatus;
            private set => m_FilesInfosStatus = value;
        }

        private void UpdateAssetDataFiles (IAssetData updatedAssetData)
        {
            m_Files = updatedAssetData.files.ToList();
            m_PreviewFilePath = updatedAssetData.previewFilePath;
            m_PreviewFileUrl = updatedAssetData.previewFileUrl;
            m_FilesInfosStatus = AssetDataFilesStatus.Fetched;
        }

        private AssetData(CloudAssetData cloudAsset)
        {
            m_Files = cloudAsset.GetAssetDataFiles();
            m_Name = cloudAsset.asset.Name;
            m_OrganizationId = cloudAsset.organizationId;
            m_ProjectId = cloudAsset.projectId;
            m_AssetId = cloudAsset.asset.Descriptor.AssetId.ToString();
            m_VersionId = cloudAsset.asset.Descriptor.AssetVersion.ToString();
            m_AssetType = cloudAsset.asset.Type.ConvertCloudAssetTypeToAssetType();
            m_Status = cloudAsset.asset.Status;
            m_Description = cloudAsset.asset.Description;
            m_Created = cloudAsset.asset.AuthoringInfo.Created;
            m_Updated = cloudAsset.asset.AuthoringInfo.Updated;
            m_AuthorName = cloudAsset.asset.AuthoringInfo.UpdatedBy;
            m_Tags = cloudAsset.asset.Tags.ToList();
            m_PreviewFilePath = cloudAsset.asset.PreviewFile;
            m_PreviewFileUrl = cloudAsset.previewFileUri?.ToString() ?? string.Empty;
            m_FilesInfosStatus = AssetDataFilesStatus.NotFetched;
        }

        static AssetDescriptor BuildDescriptor(OrganizationId organizationId, ProjectId projectId, AssetId assetId, AssetVersion assetVersionId)
        {
            var projectDescriptor = new ProjectDescriptor(organizationId, projectId);
            return new AssetDescriptor(projectDescriptor, assetId, assetVersionId);
        }

        public void OnBeforeSerialize()
        {
            m_CreatedDateTimeAsTicks = m_Created.Ticks;
            m_UpdatedDateTimeAsTicks = m_Updated.Ticks;
        }

        public void OnAfterDeserialize()
        {
            m_Created = new DateTime(m_CreatedDateTimeAsTicks);
            m_Updated = new DateTime(m_UpdatedDateTimeAsTicks);
        }

        internal class AssetDataFactory
        {
            public IAssetData CreateAssetData(CloudAssetData cloudAsset)
            {
                return new AssetData(cloudAsset);
            }

            public IAssetData UpdateAssetDataFilesInfo(AssetData assetData, AssetData updatedAssetData)
            {
                if (assetData == null || updatedAssetData == null)
                    return null;
                if (IsDifferent(assetData, updatedAssetData))
                    assetData.UpdateAssetDataFiles(updatedAssetData);
                return assetData;
            }

            public void UpdateFilesStatus(AssetData assetData, AssetDataFilesStatus status)
            {
                if (assetData == null) 
                    return;
                assetData.m_FilesInfosStatus = status;
            }
            
            public static bool IsDifferent(AssetData assetData1, AssetData assetData2)
            {
                if (assetData1 == null || assetData2 == null)
                    return false;
                
                return !assetData1.m_Files.SequenceEqual(assetData2.m_Files) ||
                       assetData1.m_Name != assetData2.m_Name ||
                       assetData1.m_Identifier.version != assetData2.m_Identifier.version ||
                       assetData1.m_Identifier.organizationId != assetData2.m_Identifier.organizationId ||
                       assetData1.m_Identifier.projectId != assetData2.m_Identifier.projectId ||
                       assetData1.m_Identifier.sourceId != assetData2.m_Identifier.sourceId ||
                       assetData1.m_Status != assetData2.m_Status ||
                       assetData1.m_AssetType != assetData2.m_AssetType ||
                       assetData1.m_Description != assetData2.m_Description ||
                       assetData1.m_Created != assetData2.m_Created ||
                       assetData1.m_Updated != assetData2.m_Updated ||
                       assetData1.m_AuthorName != assetData2.m_AuthorName ||
                       !assetData1.m_Tags.SequenceEqual(assetData2.m_Tags) ||
                       assetData1.m_PreviewFilePath != assetData2.m_PreviewFilePath ||
                       assetData1.m_PreviewFileUrl != assetData2.m_PreviewFileUrl;
            }
        }
    }

    internal class CloudAssetData
    {
        public IAsset asset { get; }
        public IEnumerable<CloudAssetDataFile> filesArg { get; set; }
        public string organizationId  { get; }
        public string projectId  { get; }
        public Uri previewFileUri  { get; }
       
        public CloudAssetData(IAsset asset, string organizationId, string projectId, Uri previewFileUri)
        {
            this.asset = asset;
            this.organizationId = organizationId;
            this.projectId = projectId;
            this.previewFileUri = previewFileUri;
            filesArg = new List<CloudAssetDataFile>();
        }

        public List<IAssetDataFile> GetAssetDataFiles()
        {
            var assetDataFiles = new List<IAssetDataFile>();
            var assetDataFileFactory = new AssetDataFile.AssetDataFileFactory();
            
            foreach (var cloudFile in filesArg)
            {
                assetDataFiles.Add(assetDataFileFactory.CreateAssetDataFile(cloudFile.file, cloudFile.downloadUrl));
            }

            return assetDataFiles;
        }
    }
}