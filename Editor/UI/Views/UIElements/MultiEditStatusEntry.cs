using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Multi-asset inline status entry. Shows "— Mixed" (bold/italic) when status or status flow
    /// differs across the selection; disables editing until selections align.
    /// In <see cref="EditingMode.Upload"/> mode the dropdown is permanently visible and
    /// changes are fired through <see cref="IFieldChangeHandler"/> without a confirmation popup.
    /// </summary>
    class MultiEditStatusEntry : DetailsPageEntry, IEditableEntry
    {
        public string AssetId => m_Identifiers.Count > 0 ? m_Identifiers[0].AssetId : string.Empty;
        public bool IsEditingEnabled { get; private set; }
        public bool AllowMultiSelection => true;

        public event Action<object> EntryEdited;
        public event Func<string, object, bool> IsEntryEdited;

        readonly IInlineEditService m_InlineEditService;
        readonly IFieldChangeHandler m_FieldChangeHandler;
        readonly EditField m_EditField;
        readonly Func<bool> m_IsEdited;

        List<BaseAssetData> m_Assets = new();
        List<AssetIdentifier> m_Identifiers = new();
        DropdownField m_DropdownField;
        EditingMode m_EditingMode;
        bool m_IsStatusMismatched;

        VisualElement m_ValueRow;
        VisualElement m_EditIcon;
        VisualElement m_PopupParent;
        InlineEditStateManager m_StateManager;
        InlineEditConfirmationPopupContainer m_ConfirmationPopup;
        string m_ValueWhenEditStarted;
        bool m_PopupHiddenForDropdown;
        string m_UploadInitialValue;
        EditSession m_AnalyticsSession;

        public MultiEditStatusEntry(
            string title,
            IEnumerable<BaseAssetData> selection,
            IInlineEditService inlineEditService,
            EditField editField,
            IFieldChangeHandler fieldChangeHandler = null,
            Func<bool> isEdited = null)
            : base(title, string.Empty)
        {
            Guard.RequireAtLeastOne(
                (inlineEditService, nameof(inlineEditService)),
                (fieldChangeHandler, nameof(fieldChangeHandler)));
            m_InlineEditService = inlineEditService;
            m_EditField = editField;
            m_FieldChangeHandler = fieldChangeHandler;
            m_IsEdited = isEdited;

            SetupDropdownField();
            UpdateFromSelection(selection);
            ConfigureEditing(EditingMode.ReadOnly);
        }

        void SetupDropdownField()
        {
            m_DropdownField = new DropdownField();
            m_DropdownField.AddToClassList(UssStyle.DetailsPageEntryValue);
            m_DropdownField.AddToClassList(UssStyle.DetailsPageDropdown);
            m_DropdownField.style.display = DisplayStyle.None;

            hierarchy.Add(m_DropdownField);
        }

        public void UpdateSelection(IEnumerable<BaseAssetData> selection)
        {
            UpdateFromSelection(selection);
        }

        void UpdateFromSelection(IEnumerable<BaseAssetData> selection)
        {
            m_Assets = selection?.ToList() ?? new List<BaseAssetData>();
            m_Identifiers = m_Assets.Select(a => a.Identifier).ToList();

            if (m_Assets.Count == 0)
                return;

            m_IsStatusMismatched = !MultiEditHelpers.AllShareSameStatusFlow(m_Assets) ||
                                   !MultiEditHelpers.AllShareSameStatus(m_Assets);

            if (m_IsStatusMismatched)
            {
                if (m_Text != null)
                {
                    m_Text.text = "— Mixed";
                    m_Text.style.unityFontStyleAndWeight = FontStyle.Italic;
                    tooltip = "The selected assets have different statuses or status flows. They must share the same status and status flow to be edited together.";
                }

                if (m_EditingMode == EditingMode.Upload)
                    m_DropdownField.SetEnabled(false);

                return;
            }

            tooltip = null;
            if (m_Text != null)
                m_Text.style.unityFontStyleAndWeight = FontStyle.Normal;

            if (m_EditingMode == EditingMode.Upload)
                m_DropdownField.SetEnabled(true);

            RefreshDropdownChoices();
            RefreshEditedIndicator();
        }

        void RefreshDropdownChoices()
        {
            if (m_Assets.Count == 0)
                return;

            var currentStatus = m_Assets.First().Status ?? string.Empty;
            var availableStatusNames = m_Assets.First().ReachableStatusNames?.ToList() ?? new List<string>();
            if (!string.IsNullOrWhiteSpace(currentStatus) && !availableStatusNames.Contains(currentStatus))
                availableStatusNames.Insert(0, currentStatus);

            m_DropdownField.choices = availableStatusNames;
            m_DropdownField.SetValueWithoutNotify(currentStatus);
            m_DropdownField.showMixedValue = false;

            if (m_Text != null)
                m_Text.text = currentStatus;

            if (m_EditingMode == EditingMode.Upload)
                m_UploadInitialValue = currentStatus;
        }

        async Task RefreshReachableStatusesAsync()
        {
            var tasks = m_Assets.Select(asset => asset.RefreshReachableStatusNamesAsync(CancellationToken.None));
            await Task.WhenAll(tasks);
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
            {
                SetupInlineValueRow();
                m_Text.style.display = DisplayStyle.Flex;
                m_DropdownField.style.display = DisplayStyle.None;
            }
            else if (mode == EditingMode.Upload)
            {
                ConfigureUploadMode();
            }
            else
            {
                m_Text.style.display = DisplayStyle.Flex;
                m_DropdownField.style.display = DisplayStyle.None;
            }

            style.display = DisplayStyle.Flex;
        }

        void ConfigureUploadMode()
        {
            if (m_Text != null)
                m_Text.style.display = DisplayStyle.None;

            m_DropdownField.style.display = DisplayStyle.Flex;
            m_UploadInitialValue = m_DropdownField.value;

            if (m_IsStatusMismatched)
                m_DropdownField.SetEnabled(false);

            m_DropdownField.RegisterValueChangedCallback(OnUploadDropdownChanged);
        }

        void OnUploadDropdownChanged(ChangeEvent<string> evt)
        {
            var newValue = evt.newValue;
            if (m_IsStatusMismatched)
                return;

            if (string.Equals(newValue, m_UploadInitialValue, StringComparison.Ordinal))
                return;

            m_UploadInitialValue = newValue;

            var edits = new List<AssetFieldEdit>();
            foreach (var id in m_Identifiers)
                edits.Add(new AssetFieldEdit(id, m_EditField, newValue));

            if (m_FieldChangeHandler != null)
                TaskUtils.TrackException(m_FieldChangeHandler.HandleChangesAsync(edits));
            else
                TaskUtils.TrackException(SaveAndRefreshAsync(newValue));

            EntryEdited?.Invoke(newValue);
            RefreshEditedIndicator();
        }

        async Task SaveAndRefreshAsync(string newValue)
        {
            var result = await MultiEditHelpers.SaveToAllAsync(m_InlineEditService, m_Identifiers, m_EditField, newValue);
            if (!result.AllSucceeded)
                Utilities.DevLogError($"Multi-edit save failed: {result.GetSummaryMessage()}");
        }

        void SetupInlineValueRow()
        {
            m_Text.RemoveFromHierarchy();
            m_DropdownField.RemoveFromHierarchy();

            m_ValueRow = new VisualElement();
            m_ValueRow.AddToClassList(UssStyle.InlineEditValueRow);
            if (!m_IsStatusMismatched)
                m_ValueRow.AddToClassList(UssStyle.InlineEditable);

            m_ValueRow.Add(m_Text);
            m_ValueRow.Add(m_DropdownField);

            m_EditIcon = new VisualElement();
            m_EditIcon.AddToClassList(UssStyle.InlineEditIcon);
            if (!m_IsStatusMismatched)
                m_ValueRow.Add(m_EditIcon);

            hierarchy.Add(m_ValueRow);

            m_StateManager = new InlineEditStateManager(this, m_ValueRow, m_EditIcon,
                inlineEditService: m_InlineEditService);

            m_ValueRow.RegisterCallback<ClickEvent>(OnInlineValueRowClick);
            m_DropdownField.RegisterValueChangedCallback(OnInlineDropdownValueChanged);
            m_DropdownField.RegisterCallback<PointerDownEvent>(OnDropdownPointerDown);
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
            m_DropdownField.UnregisterValueChangedCallback(OnInlineDropdownValueChanged);
            m_DropdownField.UnregisterCallback<PointerDownEvent>(OnDropdownPointerDown);
            m_ConfirmationPopup?.Dispose();
            m_ConfirmationPopup = null;

            m_Text.RemoveFromHierarchy();
            m_DropdownField.RemoveFromHierarchy();
            m_ValueRow?.RemoveFromHierarchy();
            m_ValueRow = null;
            m_EditIcon = null;

            hierarchy.Add(m_Text);
            hierarchy.Add(m_DropdownField);
        }

        void OnInlineValueRowClick(ClickEvent evt)
        {
            if (!IsEditingEnabled || m_IsStatusMismatched || !m_StateManager.CanStartEdit() || m_ConfirmationPopup == null)
                return;

            // Stop propagation to prevent the click from being interpreted as a confirm action
            evt.StopPropagation();

            m_StateManager.EnterEditMode();
            m_ValueWhenEditStarted = m_Text.text;
            m_Text.style.display = DisplayStyle.None;
            m_DropdownField.style.display = DisplayStyle.Flex;
            m_DropdownField.value = m_Text.text;
            m_DropdownField.Focus();

            m_ConfirmationPopup.Show(m_ValueRow, CommitAndExit, CancelAndExit, InlineEditConfirmMode.ButtonOnly);

            // Begin analytics tracking
            m_AnalyticsSession = InlineEditAnalyticsTracker.BeginEdit(
                editField: m_EditField.ToString(),
                customType: null,
                isMulti: true,
                count: m_Identifiers.Count,
                hadMixed: m_IsStatusMismatched);

            // Refresh reachable statuses in background to ensure dropdown has all available choices
            RefreshDropdownChoicesAsync();
        }

        async void RefreshDropdownChoicesAsync()
        {
            await RefreshReachableStatusesAsync();

            // State may have changed during async operation - verify still valid
            if (m_StateManager == null || !m_StateManager.IsEditing)
                return;

            RefreshDropdownChoices();
        }

        void OnDropdownPointerDown(PointerDownEvent evt)
        {
            if (m_ConfirmationPopup == null)
                return;
            m_ConfirmationPopup.SetVisible(false);
            m_ConfirmationPopup.SetSuppressOutsideClick(true);
            m_PopupHiddenForDropdown = true;
            schedule.Execute(ShowPopupAfterDropdownClosed).StartingIn(250);
        }

        void ShowPopupAfterDropdownClosed()
        {
            if (m_PopupHiddenForDropdown && m_StateManager.IsEditing && m_ConfirmationPopup != null)
            {
                m_ConfirmationPopup.SetVisible(true);
                m_ConfirmationPopup.SetSuppressOutsideClick(false);
            }
            m_PopupHiddenForDropdown = false;
        }

        void OnInlineDropdownValueChanged(ChangeEvent<string> evt)
        {
            if (!m_StateManager.IsEditing || m_ConfirmationPopup == null)
                return;
            m_PopupHiddenForDropdown = false;
            schedule.Execute(() =>
            {
                m_ConfirmationPopup?.SetVisible(true);
                m_ConfirmationPopup?.SetSuppressOutsideClick(false);
            });
        }

        async void CommitAndExit()
        {
            // Exit editing mode immediately to prevent duplicate calls from SavePendingEdits
            if (!m_StateManager.IsEditing)
                return;

            var newValue = m_DropdownField.value;
            var valueChanged = newValue != m_ValueWhenEditStarted;

            // Exit visual editing state before async save
            ExitEditing();

            if (valueChanged)
            {
                var result = await MultiEditHelpers.SaveToAllAsync(m_InlineEditService, m_Identifiers, m_EditField, newValue);

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
                    Utilities.DevLogError($"Multi-edit save failed: {result.GetSummaryMessage()}");
                    if (result.AllFailed)
                    {
                        // Re-enter edit mode on total failure so user can retry
                        m_StateManager.EnterEditMode();
                        m_Text.style.display = DisplayStyle.None;
                        m_DropdownField.style.display = DisplayStyle.Flex;
                        m_ConfirmationPopup?.Show(m_ValueRow, CommitAndExit, CancelAndExit, InlineEditConfirmMode.ButtonOnly);
                        return;
                    }
                    // Partial success - continue but log the failures
                }

                m_Text.text = newValue;
                m_DropdownField.showMixedValue = false;

                // Refresh reachable statuses on our stored assets, then update dropdown
                await RefreshReachableStatusesAsync();
                RefreshDropdownChoices();

                EntryEdited?.Invoke(newValue);
            }
            else if (m_AnalyticsSession != null)
            {
                // No value change - track as cancelled
                InlineEditAnalyticsTracker.EndEdit(
                    session: m_AnalyticsSession,
                    outcome: EditOutcome.Cancelled,
                    valueChanged: false,
                    succeededCount: 0,
                    failedCount: 0,
                    errorCategory: null);
                m_AnalyticsSession = null;
            }
        }

        void CancelAndExit()
        {
            m_DropdownField.value = m_ValueWhenEditStarted;

            // Track analytics for cancellation
            if (m_AnalyticsSession != null)
            {
                var valueChanged = m_DropdownField.value != m_ValueWhenEditStarted;
                InlineEditAnalyticsTracker.EndEdit(
                    session: m_AnalyticsSession,
                    outcome: EditOutcome.Cancelled,
                    valueChanged: valueChanged,
                    succeededCount: 0,
                    failedCount: 0,
                    errorCategory: null);
                m_AnalyticsSession = null;
            }

            ExitEditing();
        }

        void ExitEditing()
        {
            m_PopupHiddenForDropdown = false;
            m_ConfirmationPopup?.Hide();
            m_StateManager.ExitEditMode();
            m_Text.style.display = DisplayStyle.Flex;
            m_DropdownField.style.display = DisplayStyle.None;
        }

        void RefreshEditedIndicator()
        {
            if (m_IsEdited == null || m_BorderLine == null)
                return;

            if (m_IsEdited())
            {
                m_BorderLine.style.backgroundColor = UssStyle.EditedBorderColor;
                m_DropdownField.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            else
            {
                m_BorderLine.style.backgroundColor = Color.clear;
                m_DropdownField.style.unityFontStyleAndWeight = FontStyle.Normal;
            }
        }

        public void SavePendingEdits()
        {
            if (m_StateManager?.IsEditing ?? false)
                CommitAndExit();
        }

        public void Dispose()
        {
            m_StateManager?.Dispose();
            m_StateManager = null;

            if (m_EditingMode == EditingMode.Upload)
                m_DropdownField?.UnregisterValueChangedCallback(OnUploadDropdownChanged);

            if (m_ValueRow != null)
            {
                m_ValueRow.UnregisterCallback<ClickEvent>(OnInlineValueRowClick);
                m_DropdownField.UnregisterValueChangedCallback(OnInlineDropdownValueChanged);
                m_DropdownField.UnregisterCallback<PointerDownEvent>(OnDropdownPointerDown);
            }
            m_ConfirmationPopup?.Dispose();
            m_ConfirmationPopup = null;
        }
    }
}
