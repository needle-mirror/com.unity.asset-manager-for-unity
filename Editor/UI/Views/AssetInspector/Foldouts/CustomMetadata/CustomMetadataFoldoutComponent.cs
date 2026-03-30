using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class CustomMetadataFoldoutComponent
    {
        const string k_FoldoutName = "custom-metadata-foldout";

        readonly VisualElement m_Parent;
        readonly VisualElement m_ConfirmationPopupParent;
        readonly IInlineEditService m_InlineEditService;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IStateManager m_StateManager;
        readonly Action m_RefreshCallback;

        Foldout m_Foldout;
        VisualElement m_ContentContainer;
        BaseAssetData m_CurrentAssetData;
        EditingMode m_CurrentMode;
        List<UserInfo> m_UserInfosCache;
        Button m_AddFieldButton;

        public CustomMetadataFoldoutComponent(
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
            m_Foldout.AddToClassList("details-files-foldout");
            m_Foldout.value = stateManager?.CustomMetadataFoldoutValue ?? false;
            if (stateManager != null)
                m_Foldout.RegisterValueChangedCallback(evt => stateManager.CustomMetadataFoldoutValue = evt.newValue);

            m_ContentContainer = new VisualElement();
            m_ContentContainer.AddToClassList("details-files-list");
            m_Foldout.Add(m_ContentContainer);
            m_Parent.Add(m_Foldout);
        }

        public void RefreshUI(BaseAssetData assetData, EditingMode mode)
        {
            m_CurrentAssetData = assetData;
            m_CurrentMode = mode;

            foreach (var child in m_ContentContainer.Children().ToList())
            {
                if (child is IDisposable disposable)
                    disposable.Dispose();
            }
            m_ContentContainer.Clear();

            if (assetData == null || assetData.Identifier.IsLocal())
            {
                UIElementsUtils.SetDisplay(m_Foldout, false);
                return;
            }

            var metadataList = assetData.Metadata?.ToList() ?? new List<IMetadata>();
            var showFoldout = metadataList.Count > 0 || mode == EditingMode.Inline;

            if (!showFoldout)
            {
                UIElementsUtils.SetDisplay(m_Foldout, false);
                return;
            }

            UIElementsUtils.SetDisplay(m_Foldout, true);
            m_Foldout.text = $"{L10n.Tr(Constants.CustomUploadMetadata)} ({metadataList.Count})";

            if (mode == EditingMode.Inline)
            {
                var definitions = m_ProjectOrganizationProvider?.SelectedOrganization?.MetadataFieldDefinitions ?? new List<IMetadataFieldDefinition>();
                EnsureUserInfosCache();

                foreach (var metadata in metadataList)
                {
                    var fieldDef = definitions.FirstOrDefault(d => d.Key == metadata.FieldKey);
                    if (fieldDef == null)
                        continue;

                    var capturedFieldKey = metadata.FieldKey;
                    var entry = new EditableMetadataEntry(
                        metadata,
                        fieldDef,
                        m_InlineEditService,
                        assetData.Identifier,
                        metadata.Type == MetadataFieldType.User ? m_UserInfosCache : null,
                        onRemoved: () =>
                        {
                            // Update local asset data to remove the field since InlineEditService may update a different object instance
                            if (m_CurrentAssetData != null)
                            {
                                var updatedList = m_CurrentAssetData.Metadata.Where(m => m.FieldKey != capturedFieldKey).ToList();
                                m_CurrentAssetData.SetMetadata(updatedList);
                            }
                            m_RefreshCallback?.Invoke();
                        });
                    entry.SetConfirmationPopupParent(m_ConfirmationPopupParent);
                    entry.ConfigureEditing(EditingMode.Inline);
                    entry.EntryEdited += editedMetadata =>
                    {
                        // Update the local asset data directly since InlineEditService may update a different object instance
                        if (editedMetadata is IMetadata meta && m_CurrentAssetData != null)
                        {
                            // Preserve field order by replacing in-place
                            var updatedList = m_CurrentAssetData.Metadata
                                .Select(m => m.FieldKey == meta.FieldKey ? meta : m)
                                .ToList();
                            m_CurrentAssetData.SetMetadata(updatedList);
                        }
                        m_RefreshCallback?.Invoke();
                    };
                    entry.EditingStarted += OnEntryEditingStarted;
                    entry.EditingEnded += OnEntryEditingEnded;
                    m_ContentContainer.Add(entry);
                }

                m_AddFieldButton = new Button(ShowAddFieldPopup) { text = L10n.Tr("Add Custom Field") };
                m_AddFieldButton.AddToClassList(UssStyle.k_AddCustomFieldButton);
                m_ContentContainer.Add(m_AddFieldButton);
            }
            else
            {
                foreach (var metadata in metadataList)
                    AddReadOnlyEntry(m_ContentContainer, metadata);
            }
        }

        void AddReadOnlyEntry(VisualElement container, IMetadata metadata)
        {
            switch (metadata.Type)
            {
                case MetadataFieldType.Text:
                    var textMeta = (TextMetadata)metadata;
                    AssetInspectorUIElementHelper.AddText(container, textMeta.Name, textMeta.Value, isSelectable: true);
                    break;
                case MetadataFieldType.Boolean:
                    var booleanMeta = (BooleanMetadata)metadata;
                    AssetInspectorUIElementHelper.AddToggle(container, booleanMeta.Name, booleanMeta.Value);
                    break;
                case MetadataFieldType.Number:
                    var numberMeta = (NumberMetadata)metadata;
                    AssetInspectorUIElementHelper.AddText(container, numberMeta.Name,
                        numberMeta.Value.ToString(CultureInfo.CurrentCulture), isSelectable: true);
                    break;
                case MetadataFieldType.Timestamp:
                    var timestampMeta = (TimestampMetadata)metadata;
                    AssetInspectorUIElementHelper.AddText(container, timestampMeta.Name,
                        Utilities.DatetimeToString(timestampMeta.Value.DateTime), isSelectable: true);
                    break;
                case MetadataFieldType.Url:
                    var urlMeta = (UrlMetadata)metadata;
                    AssetInspectorUIElementHelper.AddText(container, urlMeta.Name,
                        urlMeta.Value.Uri == null ? string.Empty : urlMeta.Value.Uri.ToString(), isSelectable: true);
                    break;
                case MetadataFieldType.User:
                    var userMeta = (UserMetadata)metadata;
                    AssetInspectorUIElementHelper.AddUser(container, metadata.Name, userMeta.Value, null);
                    break;
                case MetadataFieldType.SingleSelection:
                    var singleMeta = (SingleSelectionMetadata)metadata;
                    AssetInspectorUIElementHelper.AddSelectionChips(container, metadata.Name,
                        new List<string> { singleMeta.Value }, isSelectable: true);
                    break;
                case MetadataFieldType.MultiSelection:
                    var multiMeta = (MultiSelectionMetadata)metadata;
                    AssetInspectorUIElementHelper.AddSelectionChips(container, metadata.Name, multiMeta.Value ?? new List<string>(), isSelectable: true);
                    break;
                default:
                    AssetInspectorUIElementHelper.AddText(container, metadata.Name, metadata.GetValue()?.ToString() ?? string.Empty);
                    break;
            }
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
            if (m_CurrentAssetData == null || m_CurrentAssetData.Identifier.IsLocal())
                return;

            var existingKeys = (m_CurrentAssetData.Metadata ?? Enumerable.Empty<IMetadata>()).Select(m => m.FieldKey).ToHashSet();
            var definitions = m_ProjectOrganizationProvider?.SelectedOrganization?.MetadataFieldDefinitions ?? new List<IMetadataFieldDefinition>();
            var available = definitions.Where(d => !existingKeys.Contains(d.Key)).ToList();

            if (available.Count == 0)
                return;

            var menu = new GenericMenu();
            foreach (var def in available.OrderBy(d => d.DisplayName))
            {
                var captured = def;
                menu.AddItem(new GUIContent(def.DisplayName), false, () =>
                {
                    AddFieldWithEditUI(captured);
                });
            }
            menu.ShowAsContext();
        }

        void OnEntryEditingStarted()
        {
            UIElementsUtils.SetDisplay(m_AddFieldButton, false);
        }

        void OnEntryEditingEnded()
        {
            UIElementsUtils.SetDisplay(m_AddFieldButton, true);
        }

        void AddFieldWithEditUI(IMetadataFieldDefinition fieldDef)
        {
            if (m_CurrentAssetData == null || m_CurrentAssetData.Identifier.IsLocal())
                return;

            EnsureUserInfosCache();

            var newMetadata = MetadataHelpers.CreateMetadataFromFieldDefinition(fieldDef);
            var addButtonIndex = m_ContentContainer.childCount - 1;
            if (addButtonIndex < 0)
                addButtonIndex = 0;

            EditableMetadataEntry entry = null;
            void RemoveEntry()
            {
                entry?.RemoveFromHierarchy();
            }

            entry = new EditableMetadataEntry(
                newMetadata,
                fieldDef,
                m_InlineEditService,
                m_CurrentAssetData.Identifier,
                fieldDef.Type == MetadataFieldType.User ? m_UserInfosCache : null,
                onRemoved: RemoveEntry,
                onCancelEdit: RemoveEntry);
            entry.SetConfirmationPopupParent(m_ConfirmationPopupParent);
            entry.ConfigureEditing(EditingMode.Inline);
            entry.EntryEdited += editedMetadata =>
            {
                // Update the local asset data directly since InlineEditService may update a different object instance
                if (editedMetadata is IMetadata meta && m_CurrentAssetData != null)
                {
                    // For new fields, append at the end
                    var currentList = m_CurrentAssetData.Metadata.ToList();
                    var existingIndex = currentList.FindIndex(m => m.FieldKey == meta.FieldKey);
                    if (existingIndex >= 0)
                        currentList[existingIndex] = meta;
                    else
                        currentList.Add(meta);
                    m_CurrentAssetData.SetMetadata(currentList);
                }
                m_RefreshCallback?.Invoke();
            };
            entry.EditingStarted += OnEntryEditingStarted;
            entry.EditingEnded += OnEntryEditingEnded;

            m_ContentContainer.Insert(addButtonIndex, entry);
            entry.BeginEdit();
        }
    }
}
