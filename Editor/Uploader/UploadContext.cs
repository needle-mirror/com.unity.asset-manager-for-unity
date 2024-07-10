using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class UploadContext
    {
        [SerializeField]
        UploadSettings m_Settings;
        
        [SerializeReference]
        List<IUploadAssetEntry> m_UploadAssetEntries = new();

        [SerializeField]
        List<string> m_IgnoredAssetGuids = new();

        [SerializeField]
        List<string> m_DependencyAssetGuids = new();

        static readonly string k_UploadEmbedDependenciesPrefKey = "com.unity.asset-manager-for-unity.upload-embed-dependencies";

        bool SavedEmbedDependencies
        {
            set => EditorPrefs.SetBool(k_UploadEmbedDependenciesPrefKey, value);
            get => EditorPrefs.GetBool(k_UploadEmbedDependenciesPrefKey, false);
        }

        bool m_EmbedDependenciesLoaded = false;
        bool m_EmbedDependencies;

        public IReadOnlyCollection<IUploadAssetEntry> UploadAssetEntries => m_UploadAssetEntries;

        public UploadSettings Settings => m_Settings;
        public string ProjectId => m_Settings.ProjectId;
        public string CollectionPath => m_Settings.CollectionPath;
        public List<string> IgnoredAssetGuids => m_IgnoredAssetGuids;
        public List<string> DependencyAssetGuids => m_DependencyAssetGuids;

        public bool EmbedDependencies
        {
            get
            {
                if (!m_EmbedDependenciesLoaded)
                {
                    m_EmbedDependencies = SavedEmbedDependencies;
                    m_EmbedDependenciesLoaded = true;
                }

                return m_EmbedDependencies;
            }
            set
            {
                m_EmbedDependencies = value;
                SavedEmbedDependencies = value;
            }
        }

        public event Action ProjectIdChanged;
        public event Action UploadAssetEntriesChanged;

        public UploadContext()
        {
            m_Settings = new UploadSettings();
        }

        public void SetUploadAssetEntries(IEnumerable<IUploadAssetEntry> uploadAssetEntries)
        {
            m_UploadAssetEntries.Clear();
            m_UploadAssetEntries.AddRange(uploadAssetEntries);
            UploadAssetEntriesChanged?.Invoke();
        }

        public void SetOrganizationInfo(OrganizationInfo organizationInfo)
        {
            if (organizationInfo != null)
            {
                m_Settings.OrganizationId = organizationInfo.Id;
            }
        }

        public void SetProjectId(string id)
        {
            m_Settings.ProjectId = id;
            ProjectIdChanged?.Invoke();
        }

        public void SetCollectionPath(string collection)
        {
            m_Settings.CollectionPath = collection;
        }
    }
}