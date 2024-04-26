using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IStateManager : IService
    {
        string LastSceneName { get; set; }
        float SideBarScrollValue { get; set; }
        bool CollectionsTopFolderFoldoutValue { get; set; }
        HashSet<string> CollapsedCollections { get; }
        float SideBarWidth { get; set; }
        bool DetailsFileFoldoutValue { get; set; }
        bool DependenciesFoldoutValue { get; set; }
        bool MultiSelectionFoldoutValue { get; set; }
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
        bool m_DetailsFileFoldoutValue;
        
        [SerializeField]
        bool m_DependenciesFoldoutValue;
        
        [SerializeField]
        bool m_MultiSelectionFoldoutValue;

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
        
        public bool DetailsFileFoldoutValue
        {
            get => m_DetailsFileFoldoutValue;
            set => m_DetailsFileFoldoutValue = value;
        }

        public bool DependenciesFoldoutValue
        {
            get => m_DependenciesFoldoutValue;
            set => m_DependenciesFoldoutValue = value;
        }
        
        public bool MultiSelectionFoldoutValue
        {
            get => m_MultiSelectionFoldoutValue;
            set => m_MultiSelectionFoldoutValue = value;
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
