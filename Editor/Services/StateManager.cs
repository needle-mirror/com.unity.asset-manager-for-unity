using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.AssetManager.Editor
{
    interface IStateManager : IService
    {
        string LastSceneName { get; set; }
        float SideBarScrollValue { get; set; }
        bool CollectionsTopFolderFoldoutValue { get; set; }
        HashSet<string> CollapsedCollections { get; }
        float SideBarWidth { get; set; }
        bool DetailsSourceFilesFoldoutValue { get; set; }
        bool DetailsUVCSFilesFoldoutValue { get; set; }
        bool DependenciesFoldoutValue { get; set; }
        bool MultiSelectionUnimportedFoldoutValue { get; set; }
        bool MultiSelectionImportedFoldoutValue { get; set; }
        bool MultiSelectionUploadIgnoredFoldoutValue { get; set; }
        bool MultiSelectionUploadIncludedFoldoutValue { get; set; }
    }

    [Serializable]
    class StateManager : BaseService<IStateManager>, IStateManager, ISerializationCallbackReceiver
    {
        [SerializeField]
        string[] m_SerializedCollapsedCollections = Array.Empty<string>();

        [SerializeField]
        string m_LastSceneName;

        [SerializeField]
        float m_SideBarScrollValue;

        [SerializeField]
        bool m_CollectionsTopFolderFoldoutValue;

        [SerializeField]
        float m_SideBarWidth = 160;

        [SerializeField]
        bool m_DetailsSourceFilesFoldoutValue;

        [SerializeField]
        bool m_DetailsUVCSFilesFoldoutValue;

        [SerializeField]
        bool m_DependenciesFoldoutValue;

        [SerializeField]
        bool m_MultiSelectionUnimportedFoldoutValue;
        
        [SerializeField]
        bool m_MultiSelectionImportedFoldoutValue;
        
        [SerializeField]
        bool m_MultiSelectionUploadIgnoredFoldoutValue;
        
        [SerializeField]
        bool m_MultiSelectionUploadIncludedFoldoutValue;

        HashSet<string> m_CollapsedCollections = new();

        public string LastSceneName
        {
            get => m_LastSceneName;
            set => m_LastSceneName = value;
        }

        public float SideBarScrollValue
        {
            get => m_SideBarScrollValue;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    return;

                m_SideBarScrollValue = value;
            }
        }

        public bool CollectionsTopFolderFoldoutValue
        {
            get => m_CollectionsTopFolderFoldoutValue;
            set => m_CollectionsTopFolderFoldoutValue = value;
        }

        public HashSet<string> CollapsedCollections
        {
            get => m_CollapsedCollections;
            set => m_CollapsedCollections = value;
        }

        public float SideBarWidth
        {
            get => m_SideBarWidth;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    return;

                m_SideBarWidth = value;
            }
        }

        public bool DetailsSourceFilesFoldoutValue
        {
            get => m_DetailsSourceFilesFoldoutValue;
            set => m_DetailsSourceFilesFoldoutValue = value;
        }

        public bool DetailsUVCSFilesFoldoutValue
        {
            get => m_DetailsUVCSFilesFoldoutValue;
            set => m_DetailsUVCSFilesFoldoutValue = value;
        }

        public bool DependenciesFoldoutValue
        {
            get => m_DependenciesFoldoutValue;
            set => m_DependenciesFoldoutValue = value;
        }

        public bool MultiSelectionUnimportedFoldoutValue
        {
            get => m_MultiSelectionUnimportedFoldoutValue;
            set => m_MultiSelectionUnimportedFoldoutValue = value;
        }
        
        public bool MultiSelectionImportedFoldoutValue
        {
            get => m_MultiSelectionImportedFoldoutValue;
            set => m_MultiSelectionImportedFoldoutValue = value;
        }
        
        public bool MultiSelectionUploadIgnoredFoldoutValue
        {
            get => m_MultiSelectionUploadIgnoredFoldoutValue;
            set => m_MultiSelectionUploadIgnoredFoldoutValue = value;
        }
        
        public bool MultiSelectionUploadIncludedFoldoutValue
        {
            get => m_MultiSelectionUploadIncludedFoldoutValue;
            set => m_MultiSelectionUploadIncludedFoldoutValue = value;
        }

        public void OnBeforeSerialize()
        {
            m_SerializedCollapsedCollections = CollapsedCollections.ToArray();
        }

        public void OnAfterDeserialize()
        {
            CollapsedCollections = new HashSet<string>(m_SerializedCollapsedCollections);
        }
    }
}
