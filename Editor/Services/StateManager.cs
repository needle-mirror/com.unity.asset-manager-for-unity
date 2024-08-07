using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IStateManager : IService
    {
        float SideBarScrollValue { get; set; }
        bool CollectionsTopFolderFoldoutValue { get; set; }
        HashSet<string> CollapsedCollections { get; }
        float SideBarWidth { get; set; }
        bool DetailsSourceFilesFoldoutValue { get; set; }
        bool DetailsUVCSFilesFoldoutValue { get; set; }
        bool DependenciesFoldoutValue { get; set; }
        bool[] MultiSelectionFoldoutsValues { get; set;}
    }

    [Serializable]
    class StateManager : BaseService<IStateManager>, IStateManager, ISerializationCallbackReceiver
    {
        [SerializeField]
        string[] m_SerializedCollapsedCollections = Array.Empty<string>();

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
        bool[] m_MultiSelectionFoldoutsValues = new bool[Enum.GetValues(typeof(MultiAssetDetailsPage.FoldoutName)).Cast<MultiAssetDetailsPage.FoldoutName>().Distinct().Count()];

        HashSet<string> m_CollapsedCollections = new();

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

        public bool[] MultiSelectionFoldoutsValues
        {
            get => m_MultiSelectionFoldoutsValues;
            set => m_MultiSelectionFoldoutsValues = value;
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
