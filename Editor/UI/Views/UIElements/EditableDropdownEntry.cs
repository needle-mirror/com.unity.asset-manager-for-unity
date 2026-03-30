using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class EditableDropdownEntry : DetailsPageEntry, IEditableEntry
    {
        public string AssetId { get; }
        public bool IsEditingEnabled { get; private set; }
        public bool AllowMultiSelection { get; }

        readonly IInlineEditService m_InlineEditService;
        DropdownField m_DropdownField;
        List<string> m_Options;
        string m_SelectedOption;
        EditingMode m_EditingMode;

        VisualElement m_ValueRow;
        VisualElement m_EditIcon;
        InlineEditStateManager m_StateManager;
        InlineEditConfirmationPopupContainer m_ConfirmationPopup;
        string m_ValueWhenEditStarted;
        bool m_PopupHiddenForDropdown;

        public event Action<object> EntryEdited;
        public event Func<string, object, bool> IsEntryEdited;

        public EditableDropdownEntry(string assetId, string title, string selectedValue, IEnumerable<string> options,
            bool allowSelection = false, bool allowMultiSelection = false, IInlineEditService inlineEditService = null)
            : base(title, selectedValue)
        {
            AssetId = assetId;
            AllowMultiSelection = allowMultiSelection;
            m_InlineEditService = inlineEditService;
            SetupFields(selectedValue, options);
            ConfigureEditing(EditingMode.ReadOnly);
        }

        void SetupFields(string selectedValue, IEnumerable<string> options)
        {
            m_Options = options.ToList();

            // We should always be able to re-select the currently assigned value
            if (!m_Options.Contains(selectedValue))
                m_Options.Insert(0, selectedValue);

            m_DropdownField = new DropdownField(m_Options, selectedValue);
            m_DropdownField.AddToClassList(UssStyle.DetailsPageEntryValue);
            m_DropdownField.AddToClassList(UssStyle.DetailsPageDropdown);
            m_DropdownField.RegisterValueChangedCallback(evt => OnEntryEdited(evt.newValue));
            m_DropdownField.RegisterCallback<GeometryChangedEvent>(OnDropdownGeometryChanged);
            m_DropdownField.style.display = DisplayStyle.None;

            var insertIndex = m_ClipboardButton != null ? hierarchy.IndexOf(m_ClipboardButton) : hierarchy.childCount;
            hierarchy.Insert(insertIndex, m_DropdownField);

            UpdateStyling(selectedValue);
        }

        void OnDropdownGeometryChanged(GeometryChangedEvent evt)
        {
            // Show tooltip only when the label text has been truncated
            var textElement = m_DropdownField.Q<TextElement>();
            var labelText = textElement.text ?? string.Empty;
            var measured = textElement.MeasureTextSize(
                labelText,
                float.PositiveInfinity,
                MeasureMode.Undefined,
                1,
                MeasureMode.Undefined
            );
            var available = textElement.contentRect.width;
            var isTruncated = measured.x > available;
            tooltip = isTruncated ? labelText : null;
        }

        public void ConfigureEditing(EditingMode mode)
        {
            if (m_EditingMode == mode)
                return;

            if (m_EditingMode == EditingMode.Inline && m_ValueRow != null)
                TeardownInlineValueRow();

            m_EditingMode = mode;
            IsEditingEnabled = mode != EditingMode.ReadOnly;

            if (mode == EditingMode.Inline)
            {
                SetupInlineValueRow();
                m_Text.text = m_DropdownField.value;
                m_Text.style.display = DisplayStyle.Flex;
                m_DropdownField.style.display = DisplayStyle.None;
                style.display = string.IsNullOrWhiteSpace(m_DropdownField.value) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            else
            {
                m_Text.style.display = IsEditingEnabled ? DisplayStyle.None : DisplayStyle.Flex;
                m_DropdownField.style.display = IsEditingEnabled ? DisplayStyle.Flex : DisplayStyle.None;

                if (!IsEditingEnabled)
                {
                    m_Text.text = m_DropdownField.value;
                    style.display = string.IsNullOrWhiteSpace(m_DropdownField.value) ? DisplayStyle.None : DisplayStyle.Flex;
                }
                else
                {
                    m_DropdownField.value = m_Text.text;
                    UpdateStyling(m_DropdownField.value);
                }
            }
        }

        void SetupInlineValueRow()
        {
            m_Text.RemoveFromHierarchy();
            m_DropdownField.RemoveFromHierarchy();

            m_ValueRow = new VisualElement();
            m_ValueRow.AddToClassList(UssStyle.InlineEditValueRow);
            m_ValueRow.AddToClassList(UssStyle.InlineEditable);

            m_ValueRow.Add(m_Text);
            m_ValueRow.Add(m_DropdownField);

            m_EditIcon = new VisualElement();
            m_EditIcon.AddToClassList(UssStyle.InlineEditIcon);
            m_ValueRow.Add(m_EditIcon);

            var insertIndex = m_ClipboardButton != null ? hierarchy.IndexOf(m_ClipboardButton) : hierarchy.childCount;
            hierarchy.Insert(insertIndex, m_ValueRow);

            m_StateManager = new InlineEditStateManager(this, m_ValueRow, m_EditIcon,
                inlineEditService: m_InlineEditService);

            m_ValueRow.RegisterCallback<ClickEvent>(OnInlineValueRowClick);
            m_DropdownField.RegisterValueChangedCallback(OnInlineDropdownValueChanged);
            m_DropdownField.RegisterCallback<PointerDownEvent>(OnDropdownPointerDown);
        }

        public void SetConfirmationPopupParent(VisualElement parent)
        {
            m_ConfirmationPopup?.Dispose();
            m_ConfirmationPopup = new InlineEditConfirmationPopupContainer();
            parent.Add(m_ConfirmationPopup);
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
        }

        void OnInlineValueRowClick(ClickEvent evt)
        {
            if (!IsEditingEnabled || !m_StateManager.CanStartEdit() || m_ConfirmationPopup == null)
                return;

            m_StateManager.EnterEditMode();
            m_ValueWhenEditStarted = m_Text.text;
            m_Text.style.display = DisplayStyle.None;
            m_DropdownField.style.display = DisplayStyle.Flex;
            m_DropdownField.value = m_Text.text;
            m_DropdownField.Focus();

            m_ConfirmationPopup.Show(m_ValueRow, CommitInlineDropdownAndExit, CancelInlineDropdownAndExit, InlineEditConfirmMode.ButtonOnly);
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

        void CommitInlineDropdownAndExit()
        {
            var newValue = m_DropdownField.value;
            if (newValue != m_Text.text)
            {
                m_Text.text = newValue;
                EntryEdited?.Invoke(newValue);
                UpdateStyling(newValue);
            }
            ExitInlineDropdownEditing();
        }

        void CancelInlineDropdownAndExit()
        {
            m_DropdownField.value = m_ValueWhenEditStarted;
            ExitInlineDropdownEditing();
        }

        void ExitInlineDropdownEditing()
        {
            m_PopupHiddenForDropdown = false;
            m_ConfirmationPopup?.Hide();
            m_StateManager.ExitEditMode();
            m_Text.style.display = DisplayStyle.Flex;
            m_DropdownField.style.display = DisplayStyle.None;
        }

        void OnEntryEdited(string newValue)
        {
            if (m_EditingMode == EditingMode.Inline)
                return;

            if (newValue == m_Text.text)
                return;

            m_Text.text = newValue;
            EntryEdited?.Invoke(newValue);
            UpdateStyling(newValue);
        }

        void UpdateStyling(string value)
        {
            InlineEditStyling.UpdateEditedStyling(
                m_BorderLine,
                m_DropdownField,
                m_EditingMode,
                () => IsEntryEdited?.Invoke(AssetId, value) ?? false);
        }

        public void SavePendingEdits()
        {
            if (m_StateManager?.IsEditing ?? false)
                CommitInlineDropdownAndExit();
        }

        public void Dispose()
        {
            m_StateManager?.Dispose();
            m_StateManager = null;

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
