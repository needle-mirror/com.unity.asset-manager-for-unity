using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IUploadFile
    {
        string SourcePath { get; }
        string DestinationPath { get; }
        bool IsDestinationOutsideProject { get; }
    }

    interface IUploadAsset
    {
        string Name { get; }
        string Guid { get; }
        AssetType AssetType { get; }
        IReadOnlyCollection<string> Tags { get; }
        IReadOnlyCollection<IUploadFile> Files { get; }
        IReadOnlyCollection<string> Dependencies { get; }
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

        public bool IsDestinationOutsideProject => m_SourcePath.StartsWith("Packages") || m_SourcePath.StartsWith("..");

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
        string m_Guid;

        [SerializeField]
        List<string> m_Tags;

        [SerializeReference]
        List<IUploadFile> m_Files;

        [SerializeField]
        List<string> m_Dependencies;

        [SerializeField]
        AssetType m_AssetType;

        public string Name => m_Name;
        public string Guid => m_Guid;
        public AssetType AssetType => m_AssetType;
        public IReadOnlyCollection<string> Tags => m_Tags;
        public IReadOnlyCollection<IUploadFile> Files => m_Files;
        public IReadOnlyCollection<string> Dependencies => m_Dependencies;

        public UploadAsset(string name, string assetGuid, IEnumerable<IUploadFile> files, IEnumerable<string> tags, IEnumerable<string> dependenciesGuids)
        {
            m_Name = name;
            m_Guid = assetGuid;

            m_Tags = tags.Distinct().Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            m_Files = files.ToList();

            m_Dependencies = dependenciesGuids != null ? dependenciesGuids.ToList() : new List<string>();

            var extensions = m_Files.Select(e => Path.GetExtension(e.SourcePath)).ToHashSet();
            var primaryExtension = AssetDataTypeHelper.GetAssetPrimaryExtension(extensions);
            var unityAssetType = AssetDataTypeHelper.GetUnityAssetType(primaryExtension);

            m_AssetType = unityAssetType.ConvertUnityAssetTypeToAssetType();
        }
    }
}
