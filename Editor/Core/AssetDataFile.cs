using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cloud.Assets;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal interface IAssetDataFile
    {
        string path { get; }
        string description { get; }
        IReadOnlyCollection<string> tags { get; }
        long fileSize { get; }
    }

    [Serializable]
    internal class AssetDataFile : IAssetDataFile // TODO Rename to SerializableFile
    {
        [SerializeField]
        private string m_Path;
        public string path => m_Path;
        
        [SerializeField]
        private string m_Description;
        public string description => m_Description;
        
        [SerializeField]
        private List<string> m_Tags = new ();
        public IReadOnlyCollection<string> tags => m_Tags;
        
        [SerializeField]
        private long m_FileSize;
        public long fileSize => m_FileSize;
        
        public AssetDataFile(IFile file)
        {
            m_Path = file.Descriptor.Path;
            if (file.Tags != null)
            {
                m_Tags.AddRange(file.Tags.ToList());
            }

            m_Description = file.Description;
            m_FileSize = file.SizeBytes;
        }
        
        public AssetDataFile(string path, string description, IEnumerable<string> tags, long fileSize)
        {
            m_Path = path;
            m_Description = description;
            m_Tags.AddRange(tags.ToList());
            m_FileSize = fileSize;
        }
    }
}
