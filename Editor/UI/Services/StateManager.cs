using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Serialized state for the Action HelpBox (MessageManager's help box). Used to persist across domain reload.
    /// </summary>
    [Serializable]
    struct ActionHelpBoxState
    {
        public string Content;
        public int MessageType;
        public int Category;
        public int RecommendedAction;
        public bool Dismissable;
    }

    interface IStateManager : IService
    {
        float SideBarScrollValue { get; set; }
        ISet<string> UncollapsedCollections { get; }
        float SideBarWidth { get; set; }
        bool DependenciesFoldoutValue { get; set; }
        bool CustomMetadataFoldoutValue { get; set; }
        bool MultiEditAssetMetadataFoldoutValue { get; set; }
        bool[] MultiSelectionFoldoutsValues { get; }

        string SelectedOrganizationId { get; set; }
        string SelectedOrganizationName { get; set; }
        string SelectedProjectId { get; set; }
        string SelectedCollectionPath { get; set; }

        string ActivePageTypeName { get; set; }

        bool GetFilesFoldoutValue(string key);
        void SetFilesFoldoutValue(string key, bool value);
        
        bool GetCollectionFoldoutPopulatedState(string key);
        void SetCollectionFoldoutPopulatedState(string key, bool value);

        void SetActionHelpBoxState(string content, int messageType, int category, int recommendedAction, bool dismissable);
        ActionHelpBoxState GetActionHelpBoxState();
        IReadOnlyList<string> StorageInfoDismissedOrganizationIds { get; }
        void SetStorageInfoDismissedOrganizationIds(IReadOnlyList<string> ids);
        void SetSeatWarningVisible(bool visible, string organizationId);
        bool GetSeatWarningWasVisibleForOrganization(string organizationId);
    }

    [Serializable]
    class StateManager : BaseService<IStateManager>, IStateManager, ISerializationCallbackReceiver
    {
        [Serializable]
        struct FileFoldoutState
        {
            public string Key;
            public bool Value;
        }

        [SerializeField]
        string[] m_SerializedUncollapsedCollections = Array.Empty<string>();

        [SerializeField]
        float m_SideBarScrollValue;

        [SerializeField]
        FileFoldoutState[] m_SerializedFilesFoldoutValues;
        
        [SerializeField]
        FileFoldoutState[] m_SerializedCollectionFoldoutPopulatedStates;

        [SerializeField]
        bool m_DependenciesFoldoutValue;

        [SerializeField]
        bool m_CustomMetadataFoldoutValue;

        [SerializeField]
        bool m_MultiEditAssetMetadataFoldoutValue = true;

        [SerializeField]
        bool[] m_MultiSelectionFoldoutsValues = new bool[Enum.GetValues(typeof(MultiAssetDetailsPage.FoldoutName)).Cast<MultiAssetDetailsPage.FoldoutName>().Distinct().Count()];

        [SerializeField]
        string m_SelectedOrganizationId;

        [SerializeField]
        string m_SelectedOrganizationName;

        [SerializeField]
        string m_SelectedProjectId;

        [SerializeField]
        string m_SelectedCollectionPath;

        [SerializeField]
        string m_ActivePageTypeName;

        [SerializeField]
        string m_ActionHelpBoxContent;

        [SerializeField]
        int m_ActionHelpBoxMessageType;

        [SerializeField]
        int m_ActionHelpBoxCategory;

        [SerializeField]
        int m_ActionHelpBoxRecommendedAction;

        [SerializeField]
        bool m_ActionHelpBoxDismissable;

        [SerializeField]
        List<string> m_StorageInfoDismissedOrganizationIds = new();

        [SerializeField]
        bool m_SeatWarningWasVisible;

        [SerializeField]
        string m_SeatWarningVisibleForOrganizationId;

        Dictionary<string, bool> m_FilesFoldoutValues = new();
        Dictionary<string, bool> m_CollectionFoldoutPopulatedStates = new();
        
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

        public bool DependenciesFoldoutValue
        {
            get => m_DependenciesFoldoutValue;
            set => m_DependenciesFoldoutValue = value;
        }

        public bool CustomMetadataFoldoutValue
        {
            get => m_CustomMetadataFoldoutValue;
            set => m_CustomMetadataFoldoutValue = value;
        }

        public bool MultiEditAssetMetadataFoldoutValue
        {
            get => m_MultiEditAssetMetadataFoldoutValue;
            set => m_MultiEditAssetMetadataFoldoutValue = value;
        }

        public bool[] MultiSelectionFoldoutsValues
        {
            get => m_MultiSelectionFoldoutsValues;
            set => m_MultiSelectionFoldoutsValues = value;
        }

        public string SelectedOrganizationId
        {
            get => m_SelectedOrganizationId;
            set => m_SelectedOrganizationId = value;
        }

        public string SelectedOrganizationName
        {
            get => m_SelectedOrganizationName;
            set => m_SelectedOrganizationName = value;
        }

        public string SelectedProjectId
        {
            get => m_SelectedProjectId;
            set => m_SelectedProjectId = value;
        }

        public string SelectedCollectionPath
        {
            get => m_SelectedCollectionPath;
            set => m_SelectedCollectionPath = value;
        }

        public string ActivePageTypeName
        {
            get => m_ActivePageTypeName;
            set => m_ActivePageTypeName = value;
        }

        public bool GetFilesFoldoutValue(string key)
        {
            return m_FilesFoldoutValues.GetValueOrDefault(key, false);
        }
        
        public void SetFilesFoldoutValue(string key, bool value)
        {
            m_FilesFoldoutValues[key] = value;
        }

        public bool GetCollectionFoldoutPopulatedState(string key)
        {
            return m_CollectionFoldoutPopulatedStates.GetValueOrDefault(key, true);
        }
        public void SetCollectionFoldoutPopulatedState(string key, bool value)
        {
            m_CollectionFoldoutPopulatedStates[key] = value;
        }

        public void SetActionHelpBoxState(string content, int messageType, int category, int recommendedAction, bool dismissable)
        {
            m_ActionHelpBoxContent = content;
            m_ActionHelpBoxMessageType = messageType;
            m_ActionHelpBoxCategory = category;
            m_ActionHelpBoxRecommendedAction = recommendedAction;
            m_ActionHelpBoxDismissable = dismissable;
        }

        public ActionHelpBoxState GetActionHelpBoxState()
        {
            return new ActionHelpBoxState
            {
                Content = m_ActionHelpBoxContent,
                MessageType = m_ActionHelpBoxMessageType,
                Category = m_ActionHelpBoxCategory,
                RecommendedAction = m_ActionHelpBoxRecommendedAction,
                Dismissable = m_ActionHelpBoxDismissable
            };
        }

        public IReadOnlyList<string> StorageInfoDismissedOrganizationIds =>
            m_StorageInfoDismissedOrganizationIds ?? new List<string>();

        public void SetStorageInfoDismissedOrganizationIds(IReadOnlyList<string> ids)
        {
            m_StorageInfoDismissedOrganizationIds.Clear();
            if (ids != null)
                m_StorageInfoDismissedOrganizationIds.AddRange(ids);
        }

        public void SetSeatWarningVisible(bool visible, string organizationId)
        {
            m_SeatWarningWasVisible = visible;
            m_SeatWarningVisibleForOrganizationId = organizationId;
        }

        public bool GetSeatWarningWasVisibleForOrganization(string organizationId)
        {
            if (string.IsNullOrEmpty(organizationId))
                return false;
            return m_SeatWarningWasVisible && m_SeatWarningVisibleForOrganizationId == organizationId;
        }

        public void OnBeforeSerialize()
        {
            m_SerializedUncollapsedCollections = UncollapsedCollections.ToArray();
            
            m_SerializedFilesFoldoutValues = m_FilesFoldoutValues.Select(kv => new FileFoldoutState { Key = kv.Key, Value = kv.Value }).ToArray();
            m_SerializedCollectionFoldoutPopulatedStates = m_CollectionFoldoutPopulatedStates.Select(kv => new FileFoldoutState { Key = kv.Key, Value = kv.Value }).ToArray();
        }

        public void OnAfterDeserialize()
        {
            m_UncollapsedCollections = new HashSet<string>(m_SerializedUncollapsedCollections);

            m_FilesFoldoutValues = m_SerializedFilesFoldoutValues?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, bool>();
            m_CollectionFoldoutPopulatedStates = m_SerializedCollectionFoldoutPopulatedStates?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, bool>();
        }
    }
}
