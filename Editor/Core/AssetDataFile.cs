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
        public string downloadUrl { get; }
    }

    [Serializable]
    internal class AssetDataFile : IAssetDataFile
    {
        [SerializeField]
        private string m_DownloadUrl;
        public string downloadUrl => m_DownloadUrl;
        
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

        private AssetDataFile(IFile cloudAssetFile, string downloadUrl)
        {
            m_Path = cloudAssetFile.Descriptor.Path;
            if (cloudAssetFile.Tags != null)
               m_Tags.AddRange(cloudAssetFile.Tags.ToList());
            m_Description = cloudAssetFile.Description;
            m_FileSize = cloudAssetFile.SizeBytes;
            m_DownloadUrl = downloadUrl;
        }

        internal class AssetDataFileFactory
        {
            public IAssetDataFile CreateAssetDataFile(IFile cloudAssetFile, string downloadUrl)
            {
                return new AssetDataFile(cloudAssetFile, downloadUrl);
            }
        }
    }

    internal class CloudAssetDataFile
    {
        public IFile file { get; }
        public string downloadUrl { get; }

        public CloudAssetDataFile(IFile file, string downloadUrl)
        {
            this.file = file;
            this.downloadUrl = downloadUrl;
        }
    }
}
