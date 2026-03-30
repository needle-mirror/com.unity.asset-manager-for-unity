using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Custom metadata foldout for multi-asset inline editing. Uses MetadataHelpers to determine
    /// field compatibility across selected assets. Only shows fields shared by all selected assets.
    /// </summary>
    class MultiEditCustomMetadataFoldout : IDisposable
    {
        const string k_FoldoutName = "multi-edit-custom-metadata-foldout";

        readonly VisualElement m_Parent;
        readonly VisualElement m_ConfirmationPopupParent;
        readonly IInlineEditService m_InlineEditService;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IStateManager m_StateManager;
        readonly Action m_RefreshCallback;

        Foldout m_Foldout;
        VisualElement m_ContentContainer;
        VisualElement m_PartialMetadataHelpBox;
        VisualElement m_AddFieldSection;
        LoadingIcon m_LoadingIcon;
        List<BaseAssetData> m_CurrentAssets;
        EditingMode m_CurrentMode;
        List<UserInfo> m_UserInfosCache;

        public MultiEditCustomMetadataFoldout(
            VisualElement parent,
            VisualElement confirmationPopupParent,
            IInlineEditService inlineEditService,
            IProjectOrganizationProvider projectOrganizationProvider,
            IStateManager stateManager,
            Action refreshCallback = null)
        {
            m_Parent = parent;
            m_ConfirmationPopupParent = confirmationPopupParent;
            m_InlineEditService = inlineEditService;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_StateManager = stateManager;
            m_RefreshCallback = refreshCallback;

            m_Foldout = new Foldout
            {
                name = k_FoldoutName,
                viewDataKey = k_FoldoutName,
                text = L10n.Tr(Constants.CustomUploadMetadata)
            };
            m_Foldout.AddToClassList("multi-selection-foldout");
            MultiSelectionFoldout.ApplyMultiSelectionFoldoutToggleLayout(m_Foldout.Q<Toggle>());
            m_Foldout.value = stateManager?.CustomMetadataFoldoutValue ?? false;
            if (stateManager != null)
                m_Foldout.RegisterValueChangedCallback(evt => stateManager.CustomMetadataFoldoutValue = evt.newValue);

            m_PartialMetadataHelpBox = new HelpBox(L10n.Tr(Constants.MetadataPartialEditing), HelpBoxMessageType.None);
            UIElementsUtils.Hide(m_PartialMetadataHelpBox);

            m_ContentContainer = new VisualElement();
            m_Foldout.Add(m_ContentContainer);
            m_Parent.Add(m_Foldout);
        }

        public void RefreshUI(IReadOnlyCollection<BaseAssetData> assets, EditingMode mode, bool metadataStillLoading = false)
        {
            m_CurrentAssets = assets?.ToList() ?? new List<BaseAssetData>();
            m_CurrentMode = mode;

            // Stop any active loading animation
            m_LoadingIcon?.StopAnimation();
            m_LoadingIcon = null;

            foreach (var child in m_ContentContainer.Children().ToList())
            {
                if (child is IDisposable disposable)
                    disposable.Dispose();
            }
            m_ContentContainer.Clear();

            if (m_CurrentAssets.Count == 0 || mode != EditingMode.Inline)
            {
                UIElementsUtils.SetDisplay(m_Foldout, false);
                return;
            }

            // If metadata is still loading, show loading state and return early
            if (metadataStillLoading)
            {
                UIElementsUtils.SetDisplay(m_Foldout, true);
                m_Foldout.text = L10n.Tr(Constants.CustomUploadMetadata);

                var loadingContainer = new VisualElement();
                loadingContainer.style.flexDirection = FlexDirection.Row;
                loadingContainer.style.alignItems = Align.Center;
                loadingContainer.style.paddingLeft = 4;
                loadingContainer.style.paddingTop = 4;
                loadingContainer.style.paddingBottom = 4;

                m_LoadingIcon = new LoadingIcon();
                m_LoadingIcon.style.width = 14;
                m_LoadingIcon.style.height = 14;
                m_LoadingIcon.style.marginRight = 6;
                m_LoadingIcon.PlayAnimation();
                loadingContainer.Add(m_LoadingIcon);

                var loadingLabel = new Label(L10n.Tr(Constants.MetadataLoadingSelection));
                loadingLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                loadingLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
                loadingContainer.Add(loadingLabel);

                m_ContentContainer.Add(loadingContainer);
                return;
            }

            // Determine which assets have any metadata for reference and partial warning display
            var assetsWithMetadata = m_CurrentAssets.Where(a => a.Metadata?.Any() == true).ToList();
            var loadedMetadata = assetsWithMetadata.Select(a => a.Metadata).ToList();
            bool someAssetsLackMetadata = assetsWithMetadata.Count < m_CurrentAssets.Count;

            var definitions = m_ProjectOrganizationProvider?.SelectedOrganization?.MetadataFieldDefinitions ?? new List<IMetadataFieldDefinition>();

            if (loadedMetadata.Count == 0 && definitions.Count == 0)
            {
                UIElementsUtils.SetDisplay(m_Foldout, false);
                return;
            }

            UIElementsUtils.SetDisplay(m_Foldout, true);

            // Show partial warning if assets have different fields OR if some assets have no metadata at all
            var hasSameKeys = MetadataHelpers.HasSameMetadataFieldKeys(loadedMetadata);
            UIElementsUtils.SetDisplay(m_PartialMetadataHelpBox, !hasSameKeys || someAssetsLackMetadata);

            EnsureUserInfosCache();

            IMetadataContainer referenceMetadata = loadedMetadata.FirstOrDefault();
            if (referenceMetadata == null)
            {
                m_Foldout.text = $"{L10n.Tr(Constants.CustomUploadMetadata)} (0)";
                AddAddFieldButton();
                return;
            }

            int fieldCount = 0;
            foreach (var metadata in referenceMetadata)
            {
                if (metadata == null)
                    continue;

                // Check ALL selected assets for this field - only show if every asset has it
                var matchingMetadata = new List<IMetadata>();
                bool allHaveField = true;
                foreach (var asset in m_CurrentAssets)
                {
                    var match = asset.Metadata?.FirstOrDefault(m => m.FieldKey == metadata.FieldKey);
                    if (match == null)
                    {
                        allHaveField = false;
                        break;
                    }
                    matchingMetadata.Add(match);
                }

                if (!allHaveField)
                    continue;

                var fieldDef = definitions.FirstOrDefault(d => d.Key == metadata.FieldKey);
                if (fieldDef == null)
                    continue;

                var identifiers = m_CurrentAssets.Select(a => a.Identifier).ToList();
                var entry = new MultiEditMetadataEntry(
                    fieldDef,
                    matchingMetadata,
                    identifiers,
                    m_InlineEditService,
                    metadata.Type == MetadataFieldType.User ? m_UserInfosCache : null,
                    onRemoved: () => m_RefreshCallback?.Invoke());
                entry.SetConfirmationPopupParent(m_ConfirmationPopupParent);
                entry.ConfigureEditing(EditingMode.Inline);
                m_ContentContainer.Add(entry);
                fieldCount++;
            }

            m_Foldout.text = $"{L10n.Tr(Constants.CustomUploadMetadata)} ({fieldCount})";

            AddAddFieldButton();
        }

        void AddAddFieldButton()
        {
            var addButton = new Button(ShowAddFieldPopup) { text = L10n.Tr("Add Custom Field") };
            addButton.AddToClassList(UssStyle.k_AddCustomFieldButton);

            m_AddFieldSection = new VisualElement();
            m_AddFieldSection.AddToClassList(UssStyle.k_AddCustomFieldSection);
            m_AddFieldSection.Add(addButton);

            m_ContentContainer.Add(m_AddFieldSection);
            // Warning box is placed after the button so it never shifts editable fields when it appears/disappears.
            m_ContentContainer.Add(m_PartialMetadataHelpBox);
        }

        void EnsureUserInfosCache()
        {
            if (m_UserInfosCache != null)
                return;

            m_UserInfosCache = new List<UserInfo>();
            _ = m_ProjectOrganizationProvider?.SelectedOrganization?.GetUserInfosAsync(userInfos =>
            {
                m_UserInfosCache.Clear();
                if (userInfos != null)
                    m_UserInfosCache.AddRange(userInfos);
            });
        }

        void ShowAddFieldPopup()
        {
            if (m_CurrentAssets == null || m_CurrentAssets.Count == 0)
                return;

            var definitions = m_ProjectOrganizationProvider?.SelectedOrganization?.MetadataFieldDefinitions ?? new List<IMetadataFieldDefinition>();

            var allMetadata = m_CurrentAssets.Select(a => a.Metadata).Where(m => m != null).ToList();

            var menu = new GenericMenu();
            foreach (var def in definitions.OrderBy(d => d.DisplayName))
            {
                var sharing = MetadataHelpers.GetMetadataSharingType(allMetadata, def.Key);
                if (sharing == MetadataSharingType.All)
                    continue;

                var captured = def;
                menu.AddItem(new GUIContent(def.DisplayName), false, () => AddFieldWithEditUI(captured));
            }

            if (menu.GetItemCount() == 0)
                return;

            menu.ShowAsContext();
        }

        void AddFieldWithEditUI(IMetadataFieldDefinition fieldDef)
        {
            if (m_CurrentAssets == null || m_CurrentAssets.Count == 0)
                return;

            EnsureUserInfosCache();

            var newMetadata = MetadataHelpers.CreateMetadataFromFieldDefinition(fieldDef);
            var metadataPerAsset = m_CurrentAssets.Select(_ => newMetadata.Clone()).ToList();
            var identifiers = m_CurrentAssets.Select(a => a.Identifier).ToList();

            // Insert before the add-field section so new entries appear above the button.
            var addButtonIndex = m_AddFieldSection != null
                ? m_ContentContainer.IndexOf(m_AddFieldSection)
                : m_ContentContainer.childCount;
            if (addButtonIndex < 0)
                addButtonIndex = m_ContentContainer.childCount;

            MultiEditMetadataEntry entry = null;
            void RemoveEntry()
            {
                entry?.RemoveFromHierarchy();
            }

            entry = new MultiEditMetadataEntry(
                fieldDef,
                metadataPerAsset,
                identifiers,
                m_InlineEditService,
                fieldDef.Type == MetadataFieldType.User ? m_UserInfosCache : null,
                onRemoved: RemoveEntry,
                onCancelEdit: RemoveEntry);
            entry.SetConfirmationPopupParent(m_ConfirmationPopupParent);
            entry.ConfigureEditing(EditingMode.Inline);
            entry.EntryEdited += _ => m_RefreshCallback?.Invoke();

            m_ContentContainer.Insert(addButtonIndex, entry);
            entry.BeginEdit();
        }

        public void Dispose()
        {
            m_LoadingIcon?.StopAnimation();
            m_LoadingIcon = null;

            foreach (var child in m_ContentContainer.Children().ToList())
            {
                if (child is IDisposable disposable)
                    disposable.Dispose();
            }
            m_ContentContainer.Clear();
        }
    }
}
