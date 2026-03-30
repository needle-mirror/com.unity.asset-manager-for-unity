using System;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Manages inline edit behavior for TextField entries.
    /// Provides click-to-edit functionality with confirmation popup and save/cancel operations.
    /// </summary>
    /// <remarks>
    /// Used by EditableTextEntry to provide inline editing functionality.
    /// The behavior manages the full edit lifecycle: setup, enter edit mode, save/cancel, and cleanup.
    /// </remarks>
    class InlineEditBehavior
    {
        readonly VisualElement m_Container;
        readonly VisualElement m_PopupParent;
        readonly TextField m_TextField;
        readonly Func<string, Task> m_OnSave;
        readonly Action m_OnCancel;
        readonly Action<string> m_OnValueSaved;
        readonly Action m_OnEditStarted;
        readonly IInlineEditService m_InlineEditService;
        readonly EditField? m_EditField;
        readonly string m_CustomMetadataType;

        VisualElement m_ValueRow;
        VisualElement m_EditIcon;
        InlineEditConfirmationPopupContainer m_ConfirmationPopup;
        InlineEditSavingIndicator m_SavingIndicator;

        string m_OriginalValue;
        bool m_Enabled;
        bool m_IsEditing;
        bool m_IsSaving;
        EditSession m_AnalyticsSession;

        public bool IsEditing => m_IsEditing;

        public InlineEditBehavior(
            VisualElement container,
            VisualElement popupParent,
            TextField textField,
            Func<string, Task> onSave,
            Action onCancel,
            EditField? editField = null,
            Action<string> onValueSaved = null,
            IInlineEditService inlineEditService = null,
            string customMetadataType = null,
            Action onEditStarted = null)
        {
            m_Container = container ?? throw new ArgumentNullException(nameof(container));
            m_PopupParent = popupParent ?? throw new ArgumentNullException(nameof(popupParent));
            m_TextField = textField ?? throw new ArgumentNullException(nameof(textField));
            m_OnSave = onSave ?? throw new ArgumentNullException(nameof(onSave));
            m_OnCancel = onCancel ?? throw new ArgumentNullException(nameof(onCancel));
            m_OnValueSaved = onValueSaved;
            m_OnEditStarted = onEditStarted;
            m_InlineEditService = inlineEditService;
            m_EditField = editField;
            m_CustomMetadataType = customMetadataType;
        }

        public void Initialize()
        {
            if (m_ValueRow != null)
                return;

            m_ValueRow = new VisualElement();
            m_ValueRow.AddToClassList(UssStyle.InlineEditValueRow);
            m_ValueRow.AddToClassList(UssStyle.InlineEditable);

            m_TextField.RemoveFromHierarchy();
            m_ValueRow.Add(m_TextField);

            m_EditIcon = new VisualElement();
            m_EditIcon.AddToClassList(UssStyle.InlineEditIcon);
            m_ValueRow.Add(m_EditIcon);

            m_TextField.isReadOnly = true;
            m_TextField.AddToClassList(UssStyle.InlineEditTextFieldReadonly);

            m_Container.Add(m_ValueRow);

            m_ConfirmationPopup = new InlineEditConfirmationPopupContainer();
            m_PopupParent.Add(m_ConfirmationPopup);

            m_ValueRow.RegisterCallback<ClickEvent>(OnClickToEdit);

            m_SavingIndicator = new InlineEditSavingIndicator(
                m_InlineEditService, m_EditIcon, m_ValueRow, m_Container, () => m_IsEditing);
        }

        public void SetEnabled(bool enabled)
        {
            m_Enabled = enabled;
            if (!enabled)
                CancelEdit();
        }

        public void StartEdit()
        {
            if (!m_Enabled || m_IsEditing)
                return;

            if (m_SavingIndicator?.IsSaveInProgress == true)
                return;

            m_IsEditing = true;
            m_OriginalValue = m_TextField.value;
            m_Container.AddToClassList(UssStyle.DetailsPageEntryEditing);
            m_ValueRow?.AddToClassList(UssStyle.InlineEditableEditing);

            m_TextField.isReadOnly = false;
            m_TextField.focusable = true;
            m_TextField.delegatesFocus = true;
            m_TextField.RemoveFromClassList(UssStyle.InlineEditTextFieldReadonly);

            if (m_EditIcon != null)
                m_EditIcon.style.display = DisplayStyle.None;

            m_TextField.Focus();
            m_TextField.SelectAll();

            m_ConfirmationPopup?.Show(m_ValueRow, ConfirmEdit, CancelEdit);

            // Notify caller that edit has started (for external analytics tracking)
            m_OnEditStarted?.Invoke();

            // Begin analytics tracking (only for single-asset edits)
            if (m_EditField.HasValue)
            {
                m_AnalyticsSession = InlineEditAnalyticsTracker.BeginEdit(
                    editField: m_EditField.Value.ToString(),
                    customType: m_CustomMetadataType,
                    isMulti: false,
                    count: 1,
                    hadMixed: false);
            }
        }

        public async void ConfirmEdit()
        {
            if (!m_IsEditing || m_IsSaving)
                return;

            var newValue = m_TextField.value;
            if (newValue == m_OriginalValue)
            {
                CancelEdit();
                return;
            }

            m_IsSaving = true;
            try
            {
                await m_OnSave(newValue);
                m_OnValueSaved?.Invoke(newValue);

                // Track successful completion
                if (m_AnalyticsSession != null)
                {
                    InlineEditAnalyticsTracker.EndEdit(
                        session: m_AnalyticsSession,
                        outcome: EditOutcome.Completed,
                        valueChanged: true,
                        succeededCount: 1,
                        failedCount: 0,
                        errorCategory: null);
                    m_AnalyticsSession = null;
                }

                ExitEditMode();
            }
            catch (Exception ex)
            {
                Core.Editor.Utilities.DevLogError($"Save failed: {Core.Editor.Utilities.GetUserFacingErrorMessage(ex)}");

                // Track failure
                if (m_AnalyticsSession != null)
                {
                    InlineEditAnalyticsTracker.EndEdit(
                        session: m_AnalyticsSession,
                        outcome: EditOutcome.Failed,
                        valueChanged: true,
                        succeededCount: 0,
                        failedCount: 1,
                        errorCategory: InlineEditAnalyticsTracker.CategorizeException(ex));
                    m_AnalyticsSession = null;
                }

                m_ConfirmationPopup?.Show(m_ValueRow, ConfirmEdit, CancelEdit);
            }
            finally
            {
                m_IsSaving = false;
            }
        }

        public void CancelEdit()
        {
            if (!m_IsEditing)
                return;

            // Don't allow cancel while save is in progress - the save will complete and handle cleanup
            if (m_IsSaving)
                return;

            m_TextField.value = m_OriginalValue;
            m_OnCancel?.Invoke();

            // Track cancellation
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

            ExitEditMode();
        }

        void ExitEditMode()
        {
            m_IsEditing = false;
            m_ConfirmationPopup?.Hide();
            m_Container.RemoveFromClassList(UssStyle.DetailsPageEntryEditing);
            m_ValueRow?.RemoveFromClassList(UssStyle.InlineEditableEditing);

            m_TextField.delegatesFocus = false;
            m_TextField.focusable = false;
            m_TextField.isReadOnly = true;
            m_TextField.AddToClassList(UssStyle.InlineEditTextFieldReadonly);
            m_TextField.MarkDirtyRepaint();
            bool containerWasFocusable = m_Container.focusable;
            m_Container.focusable = true;

            var focused = m_Container.panel?.focusController?.focusedElement as VisualElement;
            if (focused != null && (focused == m_Container || m_Container.Contains(focused)))
            {
                m_Container.schedule.Execute(() =>
                {
                    m_Container.Focus();
                    m_Container.focusable = containerWasFocusable;
                }).StartingIn(0);
            }
            else
            {
                m_Container.focusable = containerWasFocusable;
            }

            if (m_EditIcon != null)
                m_EditIcon.style.display = StyleKeyword.Null;
        }

        void OnClickToEdit(ClickEvent evt)
        {
            StartEdit();
        }

        public void SavePending()
        {
            if (m_IsEditing)
                ConfirmEdit();
        }

        public void Cleanup()
        {
            m_SavingIndicator?.Dispose();
            m_SavingIndicator = null;

            if (m_IsEditing)
                CancelEdit();

            m_ConfirmationPopup?.Dispose();
            m_ConfirmationPopup = null;

            if (m_ValueRow != null)
            {
                m_ValueRow.UnregisterCallback<ClickEvent>(OnClickToEdit);
            }

            m_TextField.isReadOnly = false;
            m_TextField.RemoveFromClassList(UssStyle.InlineEditTextFieldReadonly);
            m_TextField.RemoveFromHierarchy();
            m_ValueRow?.RemoveFromHierarchy();
        }
    }
}
