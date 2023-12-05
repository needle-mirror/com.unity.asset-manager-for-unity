using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal interface IStateManager : IService
    {
        string lastSceneName { get; set; }
        float sideBarScrollValue { get; set; }
        bool collectionsTopFolderFoldoutValue { get; set; }
        HashSet<string> collapsedCollections { get; }
        bool detailsFileFoldoutValue { get; set; }
        float sideBarWidth { get; set; }
    }

    [Serializable]
    internal class StateManager : BaseService<IStateManager>, IStateManager, ISerializationCallbackReceiver
    {
        [SerializeField]
        private string[] m_SerializedCollapsedCollections = Array.Empty<string>();

        [SerializeField]
        private string m_LastSceneName;
        public string lastSceneName
        {
            get => m_LastSceneName;
            set => m_LastSceneName = value;
        }

        [SerializeField]
        private float m_SideBarScrollValue;
        public float sideBarScrollValue
        {
            get => m_SideBarScrollValue;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    return;
                m_SideBarScrollValue = value;
            }
        }
        
        [SerializeField]
        private bool m_CollectionsTopFolderFoldoutValue;
        public bool collectionsTopFolderFoldoutValue
        {
            get => m_CollectionsTopFolderFoldoutValue;
            set => m_CollectionsTopFolderFoldoutValue = value;
        }

        private HashSet<string> m_CollapsedCollections = new HashSet<string>();
        public HashSet<string> collapsedCollections
        {
            get => m_CollapsedCollections;
            set => m_CollapsedCollections = value;
        }

        [SerializeField]
        private bool m_DetailsFileFoldoutValue;
        public bool detailsFileFoldoutValue
        {
            get => m_DetailsFileFoldoutValue;
            set => m_DetailsFileFoldoutValue = value;
        }
        
        [SerializeField] 
        private float m_SideBarWidth = 160;

        public float sideBarWidth
        {
            get => m_SideBarWidth;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    return;
                m_SideBarWidth = value;
            }
        }

        public void OnBeforeSerialize()
        {
            m_SerializedCollapsedCollections = collapsedCollections.ToArray();
        }

        public void OnAfterDeserialize()
        {
            collapsedCollections = new HashSet<string>(m_SerializedCollapsedCollections);
        }
    }
}

