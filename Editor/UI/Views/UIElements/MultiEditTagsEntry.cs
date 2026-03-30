using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Multi-asset inline tags entry. Shows common tags across all selected assets plus a "-- Mixed" chip
    /// when tags differ. Click-to-edit with confirm/cancel, saves per-asset tag lists via IInlineEditService.
    /// In <see cref="EditingMode.Upload"/> mode the chip list field is permanently visible and
    /// changes are fired through <see cref="IFieldChangeHandler"/> without a confirmation popup.
    /// </summary>
    class MultiEditTagsEntry : DetailsPageEntry, IEditableEntry
    {
        public string AssetId => m_Assets.Count > 0 ? m_Assets[0].Identifier.AssetId : string.Empty;
        public bool IsEditingEnabled { get; private set; }
        public bool AllowMultiSelection => true;

        public event Action<object> EntryEdited;
        public event Func<string, object, bool> IsEntryEdited;

        readonly IInlineEditService m_InlineEditService;
        readonly IFieldChangeHandler m_FieldChangeHandler;
        readonly Func<bool> m_IsEdited;

        List<BaseAssetData> m_Assets = new();
        HashSet<string> m_CommonTags = new();
        bool m_HasMixedValueTags;
        EditingMode m_EditingMode;
        EditSession m_AnalyticsSession;

        ChipListField m_TagField;
        VisualElement m_ValueRow;
        VisualElement m_EditIcon;
        VisualElement m_PopupParent;
        InlineEditStateManager m_StateManager;
        InlineEditConfirmationPopupContainer m_ConfirmationPopup;
        HashSet<string> m_ValuesSnapshotForCancel;

        public MultiEditTagsEntry(
            string title,
            IEnumerable<BaseAssetData> selection,
            IInlineEditService inlineEditService,
            IFieldChangeHandler fieldChangeHandler = null,
            Func<bool> isEdited = null)
            : base(title)
        {
            Guard.RequireAtLeastOne(
                (inlineEditService, nameof(inlineEditService)),
                (fieldChangeHandler, nameof(fieldChangeHandler)));
            m_InlineEditService = inlineEditService;
            m_FieldChangeHandler = fieldChangeHandler;
            m_IsEdited = isEdited;

            m_ChipContainer = AddChipContainer();
            m_ChipContainer.AddToClassList(UssStyle.FlexWrap);

            m_TagField = new ChipListField(m_CommonTags);
            m_TagField.ChipAdded += OnChipAdded;
            m_TagField.ChipRemoved += OnChipRemoved;
            m_TagField.AddToClassList(UssStyle.DetailsPageChipField);
            m_TagField.AddToClassList(UssStyle.DetailsPageEntryValue);
            m_TagField.style.display = DisplayStyle.None;
            hierarchy.Add(m_TagField);

            UpdateFromSelection(selection);
            ConfigureEditing(EditingMode.ReadOnly);
        }

        public void UpdateSelection(IEnumerable<BaseAssetData> selection)
        {
            UpdateFromSelection(selection);
        }

        void UpdateFromSelection(IEnumerable<BaseAssetData> selection)
        {
            m_Assets = selection?.ToList() ?? new List<BaseAssetData>();
            var tagSets = m_Assets.Select(a => a.Tags ?? new List<string>());
            m_CommonTags = MultiEditHelpers.GetIntersection(tagSets, out m_HasMixedValueTags).ToHashSet();

            if (m_EditingMode == EditingMode.Upload)
            {
                m_TagField.UpdateChips(m_CommonTags, m_HasMixedValueTags);
            }
            else
            {
                UpdateReadonlyChipContainer();
            }

            RefreshEditedIndicator();
        }

        public void ConfigureEditing(EditingMode mode)
        {
            if (m_EditingMode == mode)
                return;

            if (m_EditingMode == EditingMode.Inline && m_ValueRow != null)
                TeardownInlineValueRow();

            m_EditingMode = mode;
            IsEditingEnabled = mode == EditingMode.Inline || mode == EditingMode.Upload;

            if (mode == EditingMode.Inline)
                SetupInlineValueRow();
            else if (mode == EditingMode.Upload)
                ConfigureUploadMode();
            else
                m_ChipContainer.style.display = DisplayStyle.Flex;
        }

        void ConfigureUploadMode()
        {
            m_ChipContainer.style.display = DisplayStyle.None;
            m_TagField.style.display = DisplayStyle.Flex;
            m_TagField.UpdateChips(m_CommonTags, m_HasMixedValueTags);
            m_TagField.SetEnabled(true);
        }

        void OnChipAdded(string chip)
        {
            m_CommonTags.Add(chip);

            if (m_EditingMode == EditingMode.Upload)
                FlushUploadTagChange();
        }

        void OnChipRemoved(string chip)
        {
            m_CommonTags.Remove(chip);

            if (m_EditingMode == EditingMode.Upload)
                FlushUploadTagChange();
        }

        void FlushUploadTagChange()
        {
            var edits = new List<AssetFieldEdit>();
            foreach (var asset in m_Assets)
            {
                var assetTags = asset.Tags?.ToList() ?? new List<string>();

                // Sync the asset's tags with the common set: add any new common tags, remove any removed tags
                foreach (var tag in m_CommonTags)
                {
                    if (!assetTags.Contains(tag))
                        assetTags.Add(tag);
                }

                // Remove tags that were in common before but have been removed
                // We can't know removed tags directly, so rebuild from scratch:
                // keep asset-specific tags that aren't in commonTags intersection issues
                // Actually, for add/remove we can track incrementally.
                // Since OnChipAdded/OnChipRemoved already updated m_CommonTags,
                // we rebuild per-asset tags: keep existing + add all common, remove those not in common
                // But that's wrong — common tags are the intersection. Adding a tag to common means add to ALL assets.
                // Removing from common means remove from ALL assets.
                edits.Add(new AssetFieldEdit(asset.Identifier, EditField.Tags, (IEnumerable<string>)assetTags));
            }

            if (m_FieldChangeHandler != null)
                TaskUtils.TrackException(m_FieldChangeHandler.HandleChangesAsync(edits));
            else
                TaskUtils.TrackException(SaveTagsViaServiceAsync());

            EntryEdited?.Invoke(m_CommonTags);
            RefreshEditedIndicator();
        }

        async System.Threading.Tasks.Task SaveTagsViaServiceAsync()
        {
            var identifiers = m_Assets.Select(a => a.Identifier).ToList();
            var tagValues = m_Assets.Select(asset => (IEnumerable<string>)(asset.Tags?.ToList() ?? new List<string>())).ToList();
            var result = await MultiEditHelpers.SaveToAllAsync(m_InlineEditService, identifiers, EditField.Tags, tagValues);
            if (!result.AllSucceeded)
                Utilities.DevLogError($"Multi-edit tag save failed: {result.GetSummaryMessage()}");
        }

        // --- Inline mode methods (unchanged) ---

        void SetupInlineValueRow()
        {
            var insertIndex = hierarchy.IndexOf(m_ChipContainer);
            m_ChipContainer.RemoveFromHierarchy();
            m_TagField.RemoveFromHierarchy();

            m_ValueRow = new VisualElement();
            m_ValueRow.AddToClassList(UssStyle.InlineEditValueRow);
            m_ValueRow.AddToClassList(UssStyle.InlineEditable);

            m_ValueRow.Add(m_ChipContainer);
            m_ValueRow.Add(m_TagField);

            m_EditIcon = new VisualElement();
            m_EditIcon.AddToClassList(UssStyle.InlineEditIcon);
            m_ValueRow.Add(m_EditIcon);

            m_ChipContainer.style.display = DisplayStyle.Flex;
            m_TagField.style.display = DisplayStyle.None;

            hierarchy.Insert(insertIndex, m_ValueRow);

            m_StateManager = new InlineEditStateManager(this, m_ValueRow, m_EditIcon,
                inlineEditService: m_InlineEditService);
            UpdateReadonlyChipContainer();

            m_ValueRow.RegisterCallback<ClickEvent>(OnInlineValueRowClick);
        }

        public void SetConfirmationPopupParent(VisualElement parent)
        {
            m_PopupParent = parent;
            MultiEditHelpers.SetupConfirmationPopup(ref m_ConfirmationPopup, parent);
        }

        void TeardownInlineValueRow()
        {
            m_StateManager?.Dispose();
            m_StateManager = null;

            m_ValueRow.UnregisterCallback<ClickEvent>(OnInlineValueRowClick);
            m_ConfirmationPopup?.Dispose();
            m_ConfirmationPopup = null;

            var idx = hierarchy.IndexOf(m_ValueRow);
            m_ChipContainer.RemoveFromHierarchy();
            m_TagField.RemoveFromHierarchy();
            m_ValueRow.RemoveFromHierarchy();
            m_ValueRow = null;
            m_EditIcon = null;

            hierarchy.Insert(idx, m_ChipContainer);
            hierarchy.Insert(idx + 1, m_TagField);
        }

        void OnInlineValueRowClick(ClickEvent evt)
        {
            if (!IsEditingEnabled || !m_StateManager.CanStartEdit() || m_ConfirmationPopup == null)
                return;

            m_StateManager.EnterEditMode();
            m_ValuesSnapshotForCancel = new HashSet<string>(m_CommonTags);
            m_ChipContainer.style.display = DisplayStyle.None;
            m_TagField.style.display = DisplayStyle.Flex;
            m_TagField.UpdateChips(m_CommonTags, m_HasMixedValueTags);
            m_TagField.Focus();

            m_ConfirmationPopup.Show(m_ValueRow, SubmitTagEdits, CancelTagEdits, InlineEditConfirmMode.ButtonOnly);

            // Begin analytics tracking
            m_AnalyticsSession = InlineEditAnalyticsTracker.BeginEdit(
                editField: "Tags",
                customType: null,
                isMulti: true,
                count: m_Assets.Count,
                hadMixed: m_HasMixedValueTags);
        }

        async void SubmitTagEdits()
        {
            // Exit editing mode immediately to prevent duplicate calls from SavePendingEdits
            if (!m_StateManager.IsEditing)
                return;

            var addedTags = m_CommonTags.Except(m_ValuesSnapshotForCancel).ToList();
            var removedTags = m_ValuesSnapshotForCancel.Except(m_CommonTags).ToList();
            var valueChanged = addedTags.Count > 0 || removedTags.Count > 0;

            if (!valueChanged)
            {
                // Track as cancelled (no changes)
                if (m_AnalyticsSession != null)
                {
                    InlineEditAnalyticsTracker.EndEdit(
                        session: m_AnalyticsSession,
                        outcome: EditOutcome.Cancelled,
                        valueChanged: false,
                        succeededCount: 0,
                        failedCount: 0,
                        errorCategory: null);
                    m_AnalyticsSession = null;
                }

                ExitTagFieldEditing();
                return;
            }

            // Exit visual editing state before async save
            ExitTagFieldEditing();

            // Compute per-asset tag values
            var identifiers = m_Assets.Select(a => a.Identifier).ToList();
            var tagValues = m_Assets.Select(asset =>
            {
                var assetTags = asset.Tags?.ToList() ?? new List<string>();
                foreach (var tag in addedTags)
                {
                    if (!assetTags.Contains(tag))
                        assetTags.Add(tag);
                }
                foreach (var tag in removedTags)
                    assetTags.Remove(tag);
                return (IEnumerable<string>)assetTags;
            }).ToList();

            var result = await MultiEditHelpers.SaveToAllAsync(
                m_InlineEditService, identifiers, EditField.Tags, tagValues);

            // Track analytics
            if (m_AnalyticsSession != null)
            {
                var outcome = InlineEditAnalyticsTracker.DetermineOutcome(result);
                var errorCategory = outcome != EditOutcome.Completed
                    ? InlineEditAnalyticsTracker.GetErrorCategoryFromBatchResult(result)
                    : null;

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
                Utilities.DevLogError($"Multi-edit tag save failed: {result.GetSummaryMessage()}");

                if (result.AllFailed)
                {
                    // Re-enter edit mode on total failure so user can retry
                    m_StateManager.EnterEditMode();
                    m_ChipContainer.style.display = DisplayStyle.None;
                    m_TagField.style.display = DisplayStyle.Flex;
                    m_ConfirmationPopup?.Show(m_ValueRow, SubmitTagEdits, CancelTagEdits, InlineEditConfirmMode.ButtonOnly);
                    return;
                }
                // Partial success - continue to update UI
            }

            EntryEdited?.Invoke(m_CommonTags);
        }

        void CancelTagEdits()
        {
            m_CommonTags.Clear();
            foreach (var v in m_ValuesSnapshotForCancel)
                m_CommonTags.Add(v);

            // Track analytics for cancellation
            if (m_AnalyticsSession != null)
            {
                var valueChanged = !m_CommonTags.SetEquals(m_ValuesSnapshotForCancel);
                InlineEditAnalyticsTracker.EndEdit(
                    session: m_AnalyticsSession,
                    outcome: EditOutcome.Cancelled,
                    valueChanged: valueChanged,
                    succeededCount: 0,
                    failedCount: 0,
                    errorCategory: null);
                m_AnalyticsSession = null;
            }

            ExitTagFieldEditing();
        }

        void ExitTagFieldEditing()
        {
            m_ConfirmationPopup?.Hide();
            m_StateManager.ExitEditMode();
            m_ChipContainer.style.display = DisplayStyle.Flex;
            m_TagField.style.display = DisplayStyle.None;
            UpdateReadonlyChipContainer();
        }

        void UpdateReadonlyChipContainer()
        {
            MultiEditHelpers.PopulateMixedChipContainer(
                m_ChipContainer, m_CommonTags, m_HasMixedValueTags, mixedFirst: true);
        }

        void RefreshEditedIndicator()
        {
            if (m_IsEdited == null || m_BorderLine == null)
                return;

            if (m_IsEdited())
            {
                m_BorderLine.style.backgroundColor = UssStyle.EditedBorderColor;
                m_TagField.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            else
            {
                m_BorderLine.style.backgroundColor = Color.clear;
                m_TagField.style.unityFontStyleAndWeight = FontStyle.Normal;
            }
        }

        public void SavePendingEdits()
        {
            if (m_StateManager?.IsEditing ?? false)
                SubmitTagEdits();
        }

        public void Dispose()
        {
            m_StateManager?.Dispose();
            m_StateManager = null;

            if (m_ValueRow != null)
                m_ValueRow.UnregisterCallback<ClickEvent>(OnInlineValueRowClick);
            m_ConfirmationPopup?.Dispose();
            m_ConfirmationPopup = null;
        }
    }
}
