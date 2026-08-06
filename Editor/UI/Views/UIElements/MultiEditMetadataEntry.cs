using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Multi-asset inline custom metadata entry. Shows multi-value controls (mixed values via showMixedValue)
    /// and saves changes to all selected assets via IInlineEditService.
    /// </summary>
    class MultiEditMetadataEntry : MetadataPageEntry, IEditableEntry
    {
        public string AssetId => m_Identifiers.Count > 0 ? m_Identifiers[0].AssetId : string.Empty;
        public bool IsEditingEnabled { get; private set; }
        public bool AllowMultiSelection => true;

        public event Action<object> EntryEdited;

        // Required by IEditableEntry; multi-edit derives "edited" state internally rather than via this event.
#pragma warning disable CS0067
        public event Func<string, object, bool> IsEntryEdited;
#pragma warning restore CS0067

        readonly IInlineEditService m_InlineEditService;
        readonly IMetadataFieldDefinition m_FieldDefinition;
        readonly List<UserInfo> m_UserInfos;

        List<AssetIdentifier> m_Identifiers = new();
        List<IMetadata> m_MetadataList = new();
        EditingMode m_EditingMode;

        readonly VisualElement m_FieldContainer;
        VisualElement m_ReadOnlyView;
        VisualElement m_EditViewContainer;
        VisualElement m_EditIcon;
        InlineEditConfirmationPopupContainer m_ConfirmationPopup;
        InlineEditSavingIndicator m_SavingIndicator;

        bool m_IsEditing;
        bool m_IsSaving;
        List<IMetadata> m_EditCloneList;
        EditSession m_AnalyticsSession;
        List<IMetadata> m_OriginalMetadataList;
        readonly Action m_OnCancelEdit;

        public MultiEditMetadataEntry(
            IMetadataFieldDefinition fieldDefinition,
            List<IMetadata> metadataPerAsset,
            List<AssetIdentifier> identifiers,
            IInlineEditService inlineEditService,
            List<UserInfo> userInfos = null,
            Action onRemoved = null,
            Action onCancelEdit = null)
            : base(
                fieldDefinition?.Type == MetadataFieldType.MultiSelection ? string.Empty : fieldDefinition?.DisplayName ?? string.Empty,
                CreateMultiValueReadOnlyView(fieldDefinition, metadataPerAsset, userInfos),
                onRemoved != null ? () => OnRemoveClicked(inlineEditService, identifiers, metadataPerAsset, onRemoved) : (Action)null)
        {
            m_InlineEditService = inlineEditService ?? throw new ArgumentNullException(nameof(inlineEditService));
            m_FieldDefinition = fieldDefinition ?? throw new ArgumentNullException(nameof(fieldDefinition));
            m_UserInfos = userInfos ?? new List<UserInfo>();
            m_Identifiers = identifiers ?? new List<AssetIdentifier>();
            m_MetadataList = metadataPerAsset ?? new List<IMetadata>();
            m_OnCancelEdit = onCancelEdit;

            m_FieldContainer = this.Q(className: UssStyle.MetadataFieldAndButtonContainer);
            m_ReadOnlyView = m_FieldContainer?.Q(className: UssStyle.MetadataPageEntryMetadataField);

            ConfigureEditing(EditingMode.ReadOnly);
        }

        static void OnRemoveClicked(IInlineEditService service, List<AssetIdentifier> identifiers, List<IMetadata> metadataList, Action callback)
        {
            if (service == null || identifiers == null || metadataList == null || metadataList.Count == 0)
                return;

            var fieldKey = metadataList.First().FieldKey;
            TaskUtils.TrackException(RemoveAllAsync(service, identifiers, fieldKey, callback));
        }

        static async System.Threading.Tasks.Task RemoveAllAsync(IInlineEditService service, List<AssetIdentifier> identifiers, string fieldKey, Action callback)
        {
            foreach (var id in identifiers)
            {
                var result = await service.RemoveFieldAsync(id, fieldKey, default);
                if (!result.Success)
                {
                    Utilities.DevLogError($"Failed to remove field from asset {id.AssetId}: {result.ErrorMessage}");
                    return;
                }
            }
            callback?.Invoke();
        }

        static VisualElement CreateMultiValueReadOnlyView(IMetadataFieldDefinition fieldDefinition, List<IMetadata> metadataPerAsset, List<UserInfo> userInfos)
        {
            if (fieldDefinition == null || metadataPerAsset == null || metadataPerAsset.Count == 0)
                return new Label();

            return fieldDefinition.Type switch
            {
                MetadataFieldType.Text => CreateMultiValueField<string, TextMetadata>(metadataPerAsset, m => m.Value),
                MetadataFieldType.Number => CreateMultiValueNumberLabel(metadataPerAsset),
                MetadataFieldType.Boolean => CreateMultiValueToggle(metadataPerAsset),
                MetadataFieldType.Timestamp => CreateMultiValueTimestampLabel(metadataPerAsset),
                MetadataFieldType.Url => CreateMultiValueUrlReadOnlyView(metadataPerAsset),
                MetadataFieldType.User => CreateMultiValueUserLabel(metadataPerAsset, userInfos),
                MetadataFieldType.SingleSelection => CreateMultiValueField<string, SingleSelectionMetadata>(metadataPerAsset, m => m.Value),
                MetadataFieldType.MultiSelection => CreateMultiValueMultiSelectionLabel(metadataPerAsset),
                _ => new Label(metadataPerAsset.FirstOrDefault()?.GetValue()?.ToString() ?? string.Empty)
            };
        }

        static VisualElement CreateMultiValueUrlReadOnlyView(List<IMetadata> metadataList)
        {
            var values = metadataList.OfType<UrlMetadata>().ToList();
            if (values.Count == 0)
                return new Label();

            var allUriStrings = values.Select(m => m.Value.Uri?.ToString() ?? string.Empty).ToList();
            var allLabelStrings = values.Select(m => m.Value.Label ?? string.Empty).ToList();

            bool allSameUri = allUriStrings.All(u => string.Equals(u, allUriStrings[0], StringComparison.Ordinal));
            bool allSameLabel = allLabelStrings.All(l => string.Equals(l, allLabelStrings[0], StringComparison.Ordinal));

            if (!allSameUri)
                return new Label { text = "—" };

            var uriString = allUriStrings[0];
            if (string.IsNullOrEmpty(uriString))
                return new Label { text = allSameLabel ? allLabelStrings[0] : string.Empty };

            var displayText = allSameLabel && !string.IsNullOrEmpty(allLabelStrings[0]) ? allLabelStrings[0] : uriString;

            var label = new Label();
            label.enableRichText = true;
            label.text = $"<u>{displayText}</u>";
            label.tooltip = uriString;
            label.AddToClassList(UssStyle.UrlHyperlinkLabel);
            // Absorb all free space to the right so the label stays left-aligned regardless of
            // whether the edit icon is visible (which would otherwise compete via margin-left: auto).
            label.style.marginRight = StyleKeyword.Auto;

            // Open the URL when the hyperlink text is clicked; stop propagation to avoid triggering click-to-edit.
            label.RegisterCallback<ClickEvent>(evt =>
            {
                Application.OpenURL(uriString);
                evt.StopPropagation();
            });

            return label;
        }

        static VisualElement CreateMultiValueField<TVal, TMeta>(List<IMetadata> metadataList, Func<TMeta, TVal> getValue) where TMeta : class, IMetadata
        {
            var values = metadataList.OfType<TMeta>().Select(getValue).ToList();
            if (values.Count == 0)
                return new Label();
            return MultiEditHelpers.TryGetSharedValue(values, out var shared)
                ? new Label(shared?.ToString() ?? string.Empty)
                : new Label("—");
        }

        static VisualElement CreateMultiValueNumberLabel(List<IMetadata> metadataList)
        {
            var values = metadataList.OfType<NumberMetadata>().Select(m => m.Value).ToList();
            var label = new Label();
            if (values.Count == 0)
                return label;
            label.text = MultiEditHelpers.TryGetSharedValue(values, out var shared)
                ? shared.ToString(CultureInfo.CurrentCulture)
                : "—";
            return label;
        }

        static VisualElement CreateMultiValueToggle(List<IMetadata> metadataList)
        {
            var values = metadataList.OfType<BooleanMetadata>().Select(m => m.Value).ToList();
            var toggle = new Toggle();
            toggle.SetEnabled(false);
            if (values.Count > 0)
            {
                if (values.All(v => v == values[0]))
                    toggle.value = values[0];
                else
                    toggle.showMixedValue = true;
            }
            return toggle;
        }

        static VisualElement CreateMultiValueTimestampLabel(List<IMetadata> metadataList)
        {
            var values = metadataList.OfType<TimestampMetadata>().Select(m => m.Value.DateTime).ToList();
            var label = new Label();
            if (values.Count == 0)
                return label;
            label.text = MultiEditHelpers.TryGetSharedValue(values, out var shared)
                ? Utilities.DatetimeToString(shared)
                : "—";
            return label;
        }

        static VisualElement CreateMultiValueUserLabel(List<IMetadata> metadataList, List<UserInfo> userInfos)
        {
            var values = metadataList.OfType<UserMetadata>().Select(m => m.Value).ToList();
            var label = new Label();
            if (values.Count == 0)
                return label;
            label.text = MultiEditHelpers.TryGetSharedValue(values, out var shared)
                ? userInfos?.FirstOrDefault(u => u.UserId == shared)?.Name ?? shared
                : "—";
            return label;
        }

        static VisualElement CreateMultiValueMultiSelectionLabel(List<IMetadata> metadataList)
        {
            var allValues = metadataList.OfType<MultiSelectionMetadata>()
                .Select(m => m.Value ?? new List<string>())
                .ToList();
            var common = MultiEditHelpers.GetIntersection(allValues, out var hasMixed);
            return MultiEditHelpers.BuildMixedChipContainer(
                common, hasMixed, UssStyle.DetailsPageChipContainer);
        }

        public void ConfigureEditing(EditingMode mode)
        {
            if (m_EditingMode == mode)
                return;

            if (m_EditingMode == EditingMode.Inline && m_FieldContainer != null)
            {
                m_FieldContainer.RemoveFromClassList(UssStyle.InlineEditable);
                m_EditIcon?.RemoveFromHierarchy();
                m_EditIcon = null;
            }

            m_EditingMode = mode;
            IsEditingEnabled = mode == EditingMode.Inline;

            if (IsEditingEnabled)
            {
                SetKebabEditAction(BeginEdit);
                RegisterCallback<ClickEvent>(OnClickToEdit);
                if (m_FieldContainer != null)
                {
                    m_FieldContainer.AddToClassList(UssStyle.InlineEditable);
                    m_EditIcon = new VisualElement();
                    m_EditIcon.AddToClassList(UssStyle.InlineEditIcon);
                    // For URL fields the label uses margin-right: auto to stay left-aligned.
                    // Override the icon's CSS margin-left: auto so the two don't compete.
                    if (m_FieldDefinition.Type == MetadataFieldType.Url)
                        m_EditIcon.style.marginLeft = 0;
                    m_FieldContainer.Insert(m_FieldContainer.childCount - 1, m_EditIcon);
                }

                m_SavingIndicator = new InlineEditSavingIndicator(
                    m_InlineEditService, m_EditIcon, m_EditIcon?.parent ?? m_FieldContainer,
                    this, () => m_IsEditing);
            }
            else
            {
                m_SavingIndicator?.Dispose();
                m_SavingIndicator = null;
                SetKebabEditAction(null);
                UnregisterCallback<ClickEvent>(OnClickToEdit);
            }
        }

        public void SetConfirmationPopupParent(VisualElement popupParent)
        {
            MultiEditHelpers.SetupConfirmationPopup(ref m_ConfirmationPopup, popupParent);
        }

        void OnClickToEdit(ClickEvent evt)
        {
            var target = evt.target as VisualElement;
            while (target != null && target != this)
            {
                if (target.ClassListContains(UssStyle.KebabButton))
                    return;
                target = target.hierarchy.parent;
            }
            StartEdit();
        }

        void StartEdit()
        {
            if (!IsEditingEnabled || m_IsEditing || m_FieldContainer == null || m_ConfirmationPopup == null)
                return;

            if (m_SavingIndicator?.IsSaveInProgress == true)
                return;

            m_IsEditing = true;
            AddToClassList(UssStyle.MetadataPageEntryEditing);
            if (m_FieldDefinition.Type != MetadataFieldType.Boolean)
                AddToClassList(UssStyle.MetadataPageEntryEditingPopupBelow);
            m_FieldContainer?.AddToClassList(UssStyle.InlineEditableEditing);

            m_OriginalMetadataList = m_MetadataList.Select(m => m.Clone()).ToList();
            m_EditCloneList = m_MetadataList.Select(m => m.Clone()).ToList();

            if (m_ReadOnlyView != null)
                m_ReadOnlyView.style.display = DisplayStyle.None;
            if (m_EditIcon != null)
                m_EditIcon.style.display = DisplayStyle.None;

            m_EditViewContainer = CreateMetadataFieldForEdit(m_EditCloneList);
            m_EditViewContainer.AddToClassList(UssStyle.MetadataPageEntryMetadataField);
            m_FieldContainer.Insert(0, m_EditViewContainer);

            bool alignBeside = m_FieldDefinition.Type == MetadataFieldType.Boolean;
            var popupOffset = alignBeside ? new Vector2(24f, 0f) : Vector2.zero;
            m_ConfirmationPopup.Show(m_FieldContainer, ConfirmEdit, () => ExitEditMode(isCancel: true),
                alignWithTarget: alignBeside, positionOffset: popupOffset);

            // Begin analytics tracking
            // Check if values are mixed by comparing all metadata values
            bool hadMixed = false;
            if (m_MetadataList.Count > 1)
            {
                var firstValue = m_MetadataList[0];
                hadMixed = !m_MetadataList.All(m => MetadataValuesEqual(m, firstValue));
            }

            m_AnalyticsSession = InlineEditAnalyticsTracker.BeginEdit(
                editField: "Custom",
                customType: m_FieldDefinition.Type.ToString(),
                isMulti: true,
                count: m_Identifiers.Count,
                hadMixed: hadMixed);
        }

        static bool MetadataValuesEqual(IMetadata a, IMetadata b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            var aValue = a.GetValue();
            var bValue = b.GetValue();
            if (aValue == null && bValue == null) return true;
            if (aValue == null || bValue == null) return false;
            return aValue.Equals(bValue);
        }

        public void BeginEdit()
        {
            StartEdit();
        }

        VisualElement CreateMetadataFieldForEdit(List<IMetadata> metadataList)
        {
            return MetadataFieldFactory.CreateEditField(m_FieldDefinition, metadataList, m_UserInfos);
        }

        async void ConfirmEdit()
        {
            if (!m_IsEditing || m_IsSaving)
                return;

            m_IsSaving = true;
            try
            {
                var result = await MultiEditHelpers.SaveToAllAsync<IMetadata>(m_InlineEditService, m_Identifiers, EditField.Custom, m_EditCloneList);

                // Track analytics
                if (m_AnalyticsSession != null)
                {
                    var outcome = InlineEditAnalyticsTracker.DetermineOutcome(result);
                    var errorCategory = outcome != EditOutcome.Completed
                        ? InlineEditAnalyticsTracker.GetErrorCategoryFromBatchResult(result)
                        : null;

                    // Determine if value changed by comparing with original
                    bool valueChanged = false;
                    if (m_EditCloneList.Count == m_OriginalMetadataList.Count)
                    {
                        for (int i = 0; i < m_EditCloneList.Count; i++)
                        {
                            if (!MetadataValuesEqual(m_EditCloneList[i], m_OriginalMetadataList[i]))
                            {
                                valueChanged = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        valueChanged = true;
                    }

                    InlineEditAnalyticsTracker.EndEdit(
                        session: m_AnalyticsSession,
                        outcome: outcome,
                        valueChanged: valueChanged,
                        succeededCount: result.SucceededCount,
                        failedCount: result.FailedCount,
                        errorCategory: errorCategory);
                    m_AnalyticsSession = null;
                }

                if (!result.AllSucceeded)
                {
                    Utilities.DevLogError($"Save failed: {result.GetSummaryMessage()}");
                    if (result.AllFailed)
                    {
                        m_ConfirmationPopup?.Show(m_FieldContainer, ConfirmEdit, () => ExitEditMode(isCancel: true));
                        return;
                    }
                    // Partial success - continue to update UI for successful saves
                }

                m_MetadataList = m_EditCloneList.Select(m => m.Clone()).ToList();
                UpdateReadOnlyView();
                EntryEdited?.Invoke(m_MetadataList);
                ExitEditMode(isCancel: false);
            }
            finally
            {
                m_IsSaving = false;
            }
        }

        void UpdateReadOnlyView()
        {
            m_ReadOnlyView?.RemoveFromHierarchy();
            m_ReadOnlyView = CreateMultiValueReadOnlyView(m_FieldDefinition, m_MetadataList, m_UserInfos);
            m_ReadOnlyView.AddToClassList(UssStyle.MetadataPageEntryMetadataField);
            m_FieldContainer?.Insert(0, m_ReadOnlyView);
        }

        void ExitEditMode(bool isCancel = false)
        {
            if (!m_IsEditing)
                return;

            // Track analytics for cancellation
            if (isCancel && m_AnalyticsSession != null)
            {
                // Determine if value changed by comparing with original
                bool valueChanged = false;
                if (m_EditCloneList != null && m_OriginalMetadataList != null &&
                    m_EditCloneList.Count == m_OriginalMetadataList.Count)
                {
                    for (int i = 0; i < m_EditCloneList.Count; i++)
                    {
                        if (!MetadataValuesEqual(m_EditCloneList[i], m_OriginalMetadataList[i]))
                        {
                            valueChanged = true;
                            break;
                        }
                    }
                }

                InlineEditAnalyticsTracker.EndEdit(
                    session: m_AnalyticsSession,
                    outcome: EditOutcome.Cancelled,
                    valueChanged: valueChanged,
                    succeededCount: 0,
                    failedCount: 0,
                    errorCategory: null);
                m_AnalyticsSession = null;
            }

            m_IsEditing = false;
            m_ConfirmationPopup?.Hide();
            RemoveFromClassList(UssStyle.MetadataPageEntryEditing);
            RemoveFromClassList(UssStyle.MetadataPageEntryEditingPopupBelow);
            m_FieldContainer?.RemoveFromClassList(UssStyle.InlineEditableEditing);

            m_EditViewContainer?.RemoveFromHierarchy();
            if (m_ReadOnlyView != null)
                m_ReadOnlyView.style.display = DisplayStyle.Flex;
            if (m_EditIcon != null)
                m_EditIcon.style.display = StyleKeyword.Null;

            m_EditViewContainer = null;
            m_EditCloneList = null;

            if (isCancel)
                m_OnCancelEdit?.Invoke();
        }

        public void SavePendingEdits()
        {
            if (m_IsEditing)
                ConfirmEdit();
        }

        public void Dispose()
        {
            m_SavingIndicator?.Dispose();
            m_SavingIndicator = null;
            UnregisterCallback<ClickEvent>(OnClickToEdit);
            m_ConfirmationPopup?.Dispose();
            m_ConfirmationPopup = null;
        }
    }
}
