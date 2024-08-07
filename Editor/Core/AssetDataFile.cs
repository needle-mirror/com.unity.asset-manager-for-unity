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
        string Extension { get; }
        bool Available { get; }
        string Description { get; }
        IReadOnlyCollection<string> Tags { get; }
        long FileSize { get; }
        string Guid { get; }
    }

    [Serializable]
    class AssetDataFile : IAssetDataFile
    {
        [SerializeField]
        string m_Path;
        
        [SerializeField]
        string m_Extension;

        [SerializeField]
        bool m_Available;

        [SerializeField]
        string m_Description;

        [SerializeField]
        List<string> m_Tags = new();

        [SerializeField]
        long m_FileSize;

        [SerializeField]
        string m_Guid;

        public string Path => m_Path;
        public string Extension => m_Extension;
        public bool Available => m_Available;
        public string Description => m_Description;
        public IReadOnlyCollection<string> Tags => m_Tags;
        public long FileSize => m_FileSize;
        public string Guid => m_Guid;

        public AssetDataFile(IFile file)
        {
            if (string.IsNullOrEmpty(file.Descriptor.Path)) 
                return;

            m_Path = file.Descriptor.Path;
            m_Extension = System.IO.Path.GetExtension(m_Path).ToLower();

            if (file.Tags != null)
            {
                m_Tags.AddRange(file.Tags.ToList());
            }

            m_Available = string.IsNullOrEmpty(file.Status) ||
                          file.Status.Equals("Uploaded", StringComparison.OrdinalIgnoreCase);
            m_Description = file.Description ?? string.Empty;
            m_FileSize = file.SizeBytes;
            m_Guid = null;
        }

        public AssetDataFile(string path, string extension, string guid, string description, IEnumerable<string> tags, long fileSize, bool available)
        {
            m_Path = path;
            m_Extension = extension;
            m_Guid = guid;
            m_Available = available;
            m_Description = description;
            m_Tags.AddRange(tags.ToList());
            m_FileSize = fileSize;
        }
    }
}
