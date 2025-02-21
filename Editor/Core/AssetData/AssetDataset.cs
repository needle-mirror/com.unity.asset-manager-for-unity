using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    [Serializable]
    class AssetDataset
    {
        [SerializeField]
        string m_Id;

        [SerializeField]
        string m_Name;

        [SerializeField]
        List<string> m_SystemTags;

        [SerializeReference]
        List<BaseAssetDataFile> m_Files;

        public string Id => m_Id;
        public string Name => m_Name;
        public IEnumerable<string> SystemTags => m_SystemTags ?? new List<string>();

        public List<BaseAssetDataFile> Files
        {
            get => m_Files ?? new List<BaseAssetDataFile>();
            internal set => m_Files = value;
        }

        public bool IsSourceControlled => m_SystemTags.Contains("SourceControl");

        internal AssetDataset(string id, string name, IEnumerable<string> systemTags)
        {
            m_Id = id;
            m_Name = name;
            m_SystemTags = systemTags?.ToList();
        }

        internal AssetDataset(string name, IEnumerable<string> systemTags, IEnumerable<BaseAssetDataFile> files)
        {
            m_Id = string.Empty;
            m_Name = name;
            m_SystemTags = systemTags?.ToList();
            m_Files = files?.ToList();
        }

        internal void Copy(AssetDataset other)
        {
            m_Id = other.Id;
            m_Name = other.Name;
            m_SystemTags = new List<string>();
            m_SystemTags.AddRange(other.SystemTags);
        }
    }
}
