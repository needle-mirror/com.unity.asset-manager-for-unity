using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Multi-asset inline text entry (e.g. Description). Shows mixed value when selections differ,
    /// click-to-edit with confirm/cancel, and saves to all selected assets via IInlineEditService.
    /// In <see cref="EditingMode.Upload"/> mode the text field is permanently visible and
    /// changes are fired through <see cref="IFieldChangeHandler"/> without a confirmation popup.
    /// </summary>
    class MultiEditTextEntry : DetailsPageEntry, IEditableEntry
    {
        public string AssetId => m_Identifiers.Count > 0 ? m_Identifiers[0].AssetId : string.Empty;
        public bool IsEditingEnabled { get; private set; }
        public bool AllowMultiSelection => true;

        public event Action<object> EntryEdited;

        // Required by IEditableEntry; multi-edit uses the m_IsEdited Func<bool> passed via the constructor
        // to compute "edited" state across the selection, so this event is intentionally never raised here.
#pragma warning disable CS0067
        public event Func<string, object, bool> IsEntryEdited;
#pragma warning restore CS0067

        readonly IInlineEditService m_InlineEditService;
        readonly IFieldChangeHandler m_FieldChangeHandler;
        readonly EditField m_EditField;
        readonly Func<bool> m_IsEdited;
        VisualElement m_PopupParent;

        List<AssetIdentifier> m_Identifiers = new();
        TextField m_TextField;
        InlineEditBehavior m_InlineEditBehavior;
        EditingMode m_EditingMode;
        string m_UploadInitialValue;
        EditSession m_AnalyticsSession;
        string m_OriginalValue;

        public MultiEditTextEntry(
            string title,
            IEnumerable<BaseAssetData> selection,
            IInlineEditService inlineEditService,
            EditField editField,
            IFieldChangeHandler fieldChangeHandler = null,
            Func<bool> isEdited = null)
            : base(title, GetDisplayText(selection), allowSelection: false)
        {
            Guard.RequireAtLeastOne(
                (inlineEditService, nameof(inlineEditService)),
                (fieldChangeHandler, nameof(fieldChangeHandler)));
            m_InlineEditService = inlineEditService;
            m_EditField = editField;
            m_FieldChangeHandler = fieldChangeHandler;
            m_IsEdited = isEdited;

            SetupTextField(selection);
            ConfigureEditing(EditingMode.ReadOnly);
        }

        static string GetDisplayText(IEnumerable<BaseAssetData> selection)
        {
            var list = selection?.Select(a => a?.Description).ToList() ?? new List<string>();
            if (list.Count == 0)
                return string.Empty;
            var first = list[0];
            if (list.All(v => string.Equals(v, first, StringComparison.Ordinal)))
                return first ?? string.Empty;
            return string.Empty;
        }

        void SetupTextField(IEnumerable<BaseAssetData> selection)
        {
            m_TextField = new TextField
            {
                label = string.Empty
            };
            m_TextField.AddToClassList(UssStyle.DetailsPageEntryValue);
            m_TextField.style.display = DisplayStyle.None;

            hierarchy.Add(m_TextField);
            UpdateFromSelection(selection);
        }

        /// <summary>
        /// Updates the entry when the selected assets change.
        /// </summary>
        public void UpdateSelection(IEnumerable<BaseAssetData> selection)
        {
            UpdateFromSelection(selection);
        }

        void UpdateFromSelection(IEnumerable<BaseAssetData> selection)
        {
            var list = selection?.ToList() ?? new List<BaseAssetData>();
            m_Identifiers = list.Select(a => a.Identifier).ToList();
            var descriptions = list.Select(a => a.Description).ToList();

            // Don't update the text field if user is actively editing - this prevents
            // data refreshes from resetting the user's in-progress changes
            if (m_TextField != null && m_InlineEditBehavior?.IsEditing != true)
            {
                m_TextField.InitializeFieldAsMultiValue(descriptions);
                if (m_Text != null)
                    m_Text.text = m_TextField.showMixedValue ? m_TextField.value : (m_TextField.value ?? string.Empty);
            }

            if (m_EditingMode == EditingMode.Upload)
                m_UploadInitialValue = m_TextField?.value;

            RefreshEditedIndicator();
        }

        public void ConfigureEditing(EditingMode mode)
        {
            if (m_EditingMode == mode)
                return;

            m_EditingMode = mode;
            IsEditingEnabled = mode == EditingMode.Inline || mode == EditingMode.Upload;

            if (mode == EditingMode.Inline)
                ConfigureInlineMode();
            else if (mode == EditingMode.Upload)
                ConfigureUploadMode();
            else
                ConfigureReadOnlyMode();
        }

        void ConfigureInlineMode()
        {
            if (m_InlineEditBehavior == null)
            {
                m_Text.RemoveFromHierarchy();
                m_TextField.RemoveFromHierarchy();
                m_TextField.style.display = DisplayStyle.Flex;
                m_TextField.label = string.Empty;

                m_InlineEditBehavior = new InlineEditBehavior(
                    container: this,
                    popupParent: m_PopupParent ?? this,
                    textField: m_TextField,
                    onSave: SaveAllAsync,
                    onCancel: OnCancelEdit,
                    editField: null, // Multi-edit tracks analytics separately
                    onValueSaved: OnValueSaved,
                    inlineEditService: m_InlineEditService,
                    onEditStarted: OnEditStarted);

                m_InlineEditBehavior.Initialize();
            }

            m_InlineEditBehavior.SetEnabled(true);
            style.display = DisplayStyle.Flex;
        }

        void OnEditStarted()
        {
            // Begin analytics tracking for multi-edit when user actually starts editing
            m_OriginalValue = m_TextField.value;
            m_AnalyticsSession = InlineEditAnalyticsTracker.BeginEdit(
                editField: m_EditField.ToString(),
                customType: null,
                isMulti: true,
                count: m_Identifiers.Count,
                hadMixed: m_TextField.showMixedValue);
        }

        void ConfigureUploadMode()
        {
            if (m_Text != null)
                m_Text.style.display = DisplayStyle.None;

            m_TextField.style.display = DisplayStyle.Flex;
            m_TextField.label = string.Empty;
            m_UploadInitialValue = m_TextField.value;

            m_TextField.RegisterCallback<KeyUpEvent>(OnUploadKeyUp);
            m_TextField.RegisterCallback<FocusOutEvent>(OnUploadFocusOut);

            style.display = DisplayStyle.Flex;
        }

        void OnUploadKeyUp(KeyUpEvent evt)
        {
            if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                FlushUploadValue();
        }

        void OnUploadFocusOut(FocusOutEvent evt)
        {
            FlushUploadValue();
        }

        void FlushUploadValue()
        {
            if (m_TextField == null)
                return;

            var newValue = m_TextField.value;
            if (string.Equals(newValue, m_UploadInitialValue, StringComparison.Ordinal))
                return;

            m_UploadInitialValue = newValue;

            var edits = new List<AssetFieldEdit>();
            foreach (var id in m_Identifiers)
                edits.Add(new AssetFieldEdit(id, m_EditField, newValue));

            if (m_FieldChangeHandler != null)
                TaskUtils.TrackException(m_FieldChangeHandler.HandleChangesAsync(edits));
            else
                TaskUtils.TrackException(SaveAllAsync(newValue));

            EntryEdited?.Invoke(newValue);
            RefreshEditedIndicator();
        }

        void ConfigureReadOnlyMode()
        {
            if (m_InlineEditBehavior != null)
            {
                m_InlineEditBehavior.Cleanup();
                m_InlineEditBehavior = null;

                var titleIndex = hierarchy.IndexOf(m_Title);
                hierarchy.Insert(titleIndex + 1, m_Text);
                hierarchy.Insert(titleIndex + 2, m_TextField);
                m_TextField.value = m_Text.text;
            }

            m_TextField.style.display = DisplayStyle.None;
            m_Text.style.display = DisplayStyle.Flex;
            style.display = DisplayStyle.Flex;
        }

        void OnValueSaved(string newValue)
        {
            m_Text.text = newValue;
            m_TextField.showMixedValue = false;
            m_TextField.value = newValue;
            EntryEdited?.Invoke(newValue);
        }

        public void SetConfirmationPopupParent(VisualElement parent)
        {
            m_PopupParent = parent;
        }

        async Task SaveAllAsync(string newValue)
        {
            var result = await MultiEditHelpers.SaveToAllAsync(m_InlineEditService, m_Identifiers, m_EditField, newValue);

            // Track analytics
            if (m_AnalyticsSession != null)
            {
                var outcome = InlineEditAnalyticsTracker.DetermineOutcome(result);
                var errorCategory = outcome != EditOutcome.Completed
                    ? InlineEditAnalyticsTracker.GetErrorCategoryFromBatchResult(result)
                    : null;
                var valueChanged = newValue != m_OriginalValue;

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
                    throw new Exception(result.GetSummaryMessage());
                // Partial success - log but don't throw so UI updates for successful saves
            }
        }

        void OnCancelEdit()
        {
            // Track analytics for cancellation
            if (m_AnalyticsSession != null)
            {
                var valueChanged = m_TextField.value != m_OriginalValue;
                InlineEditAnalyticsTracker.EndEdit(
                    session: m_AnalyticsSession,
                    outcome: EditOutcome.Cancelled,
                    valueChanged: valueChanged,
                    succeededCount: 0,
                    failedCount: 0,
                    errorCategory: null);
                m_AnalyticsSession = null;
            }
        }

        void RefreshEditedIndicator()
        {
            if (m_IsEdited == null || m_BorderLine == null)
                return;

            if (m_IsEdited())
            {
                m_BorderLine.style.backgroundColor = UssStyle.EditedBorderColor;
                m_TextField.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            else
            {
                m_BorderLine.style.backgroundColor = Color.clear;
                m_TextField.style.unityFontStyleAndWeight = FontStyle.Normal;
            }
        }

        public void SavePendingEdits()
        {
            if (m_EditingMode == EditingMode.Upload)
            {
                FlushUploadValue();
                return;
            }

            if (!IsEditingEnabled)
                return;
            m_InlineEditBehavior?.SavePending();
        }

        public void Dispose()
        {
            if (m_EditingMode == EditingMode.Upload)
            {
                m_TextField?.UnregisterCallback<KeyUpEvent>(OnUploadKeyUp);
                m_TextField?.UnregisterCallback<FocusOutEvent>(OnUploadFocusOut);
            }

            m_InlineEditBehavior?.Cleanup();
            m_InlineEditBehavior = null;
        }
    }
}
