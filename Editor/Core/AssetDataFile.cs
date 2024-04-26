using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cloud.Assets;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IAssetDataFile
    {
        string Path { get; }
        string Description { get; }
        IReadOnlyCollection<string> Tags { get; }
        long FileSize { get; }
        string Guid { get; }
    }

    [Serializable]
    class AssetDataFile : IAssetDataFile // TODO Rename to SerializableFile
    {
        [SerializeField]
        string m_Path;

        [SerializeField]
        string m_Description;

        [SerializeField]
        List<string> m_Tags = new();

        [SerializeField]
        long m_FileSize;

        [SerializeField]
        string m_Guid;

        public string Path => m_Path;
        public string Description => m_Description;
        public IReadOnlyCollection<string> Tags => m_Tags;
        public long FileSize => m_FileSize;
        public string Guid => m_Guid;

        public AssetDataFile(IFile file)
        {
            m_Path = file.Descriptor.Path;
            if (file.Tags != null)
            {
                m_Tags.AddRange(file.Tags.ToList());
            }

            m_Description = file.Description;
            m_FileSize = file.SizeBytes;
            m_Guid = null;
        }

        public AssetDataFile(string path, string guid, string description, IEnumerable<string> tags, long fileSize)
        {
            m_Path = path;
            m_Guid = guid;
            m_Description = description;
            m_Tags.AddRange(tags.ToList());
            m_FileSize = fileSize;
        }
    }
}
