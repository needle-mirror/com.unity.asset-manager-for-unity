using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    interface IStateManager : IService
    {
        float SideBarScrollValue { get; set; }
        ISet<string> UncollapsedCollections { get; }
        float SideBarWidth { get; set; }
        bool DetailsSourceFilesFoldoutValue { get; set; }
        bool DetailsUVCSFilesFoldoutValue { get; set; }
        bool DependenciesFoldoutValue { get; set; }
        bool[] MultiSelectionFoldoutsValues { get; }
    }

    [Serializable]
    class StateManager : BaseService<IStateManager>, IStateManager, ISerializationCallbackReceiver
    {
        [SerializeField]
        string[] m_SerializedUncollapsedCollections = Array.Empty<string>();

        [SerializeField]
        float m_SideBarScrollValue;

        [SerializeField]
        bool m_DetailsSourceFilesFoldoutValue;

        [SerializeField]
        bool m_DetailsUVCSFilesFoldoutValue;

        [SerializeField]
        bool m_DependenciesFoldoutValue;

        [SerializeField]
        bool[] m_MultiSelectionFoldoutsValues = new bool[Enum.GetValues(typeof(MultiAssetDetailsPage.FoldoutName)).Cast<MultiAssetDetailsPage.FoldoutName>().Distinct().Count()];

        static readonly string k_SideBarWidthPrefKey = "com.unity.asset-manager-for-unity.side-bar-width";

        HashSet<string> m_UncollapsedCollections = new();

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

        public ISet<string> UncollapsedCollections => m_UncollapsedCollections;

        public float SideBarWidth
        {
            get => EditorPrefs.GetFloat(k_SideBarWidthPrefKey, 160.0f);
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    return;

                EditorPrefs.SetFloat(k_SideBarWidthPrefKey, value);
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
            m_SerializedUncollapsedCollections = UncollapsedCollections.ToArray();
        }

        public void OnAfterDeserialize()
        {
            m_UncollapsedCollections = new HashSet<string>(m_SerializedUncollapsedCollections);
        }
    }
}
