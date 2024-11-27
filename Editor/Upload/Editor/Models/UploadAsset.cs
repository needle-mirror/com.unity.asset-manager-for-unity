using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.Upload.Editor
{
    interface IUploadFile
    {
        string SourcePath { get; }
        string DestinationPath { get; }
    }

    interface IUploadAsset
    {
        string Name { get; }
        AssetType AssetType { get; }
        IReadOnlyCollection<string> Tags { get; }
        IReadOnlyCollection<IUploadFile> Files { get; }
        IReadOnlyCollection<AssetIdentifier> Dependencies { get; }

        AssetIdentifier LocalIdentifier { get; }

        /// <summary>
        /// If this IUploadAsset is an update to an existing asset, this will contain the identifier of the existing asset.
        /// Otherwise, it will be null.
        /// </summary>
        AssetIdentifier ExistingAssetIdentifier { get; }

        ProjectIdentifier TargetProject { get; }
        string TargetCollection { get; }

        // The GUID of the asset that will be used to generate the thumbnail
        string PreviewGuid { get; }
    }

    [Serializable]
    class UploadFile : IUploadFile
    {
        [SerializeField]
        string m_SourcePath;

        [SerializeField]
        string m_DestinationPath;

        public string SourcePath => m_SourcePath;
        public string DestinationPath => m_DestinationPath;

        public UploadFile(string sourcePath, string destinationPath)
        {
            m_SourcePath = sourcePath;
            m_DestinationPath = destinationPath;
        }
    }

    [Serializable]
    class UploadAsset : IUploadAsset
    {
        [SerializeField]
        string m_Name;

        [SerializeField]
        AssetIdentifier m_LocalIdentifier;

        [SerializeField]
        string m_PreviewGuid;

        [SerializeField]
        List<string> m_Tags;

        [SerializeReference]
        List<IUploadFile> m_Files;

        [SerializeField]
        List<AssetIdentifier> m_Dependencies;

        [SerializeField]
        AssetType m_AssetType;

        [SerializeField]
        AssetIdentifier m_OriginalAssetData;

        [SerializeField]
        ProjectIdentifier m_TargetProject;

        [SerializeField]
        string m_TargetCollection;

        public string Name => m_Name;
        public string PreviewGuid => m_PreviewGuid;
        public AssetIdentifier LocalIdentifier => m_LocalIdentifier;
        public AssetType AssetType => m_AssetType;
        public IReadOnlyCollection<string> Tags => m_Tags;
        public IReadOnlyCollection<IUploadFile> Files => m_Files;
        public IReadOnlyCollection<AssetIdentifier> Dependencies => m_Dependencies;

        public AssetIdentifier ExistingAssetIdentifier => m_OriginalAssetData;
        public ProjectIdentifier TargetProject => m_TargetProject;
        public string TargetCollection => m_TargetCollection;

        public UploadAsset(string name, string previewGuid, AssetIdentifier localIdentifier, AssetType assetType,
            IEnumerable<IUploadFile> files, IEnumerable<string> tags, IEnumerable<AssetIdentifier> dependencies,
            AssetIdentifier originalAssetData, ProjectIdentifier targetProject, string targetCollection)
        {
            m_Name = name;
            m_PreviewGuid = previewGuid;
            m_LocalIdentifier = localIdentifier;
            m_AssetType = assetType;

            m_Tags = tags.Distinct().Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            m_Files = files.ToList();

            m_Dependencies = dependencies != null ? dependencies.ToList() : new List<AssetIdentifier>();

            m_OriginalAssetData = originalAssetData;
            m_TargetProject = targetProject;
            m_TargetCollection = targetCollection;
        }
    }
}
