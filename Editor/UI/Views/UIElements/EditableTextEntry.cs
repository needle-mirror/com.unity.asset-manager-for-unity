using System;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class EditableTextEntry : DetailsPageEntry, IEditableEntry
    {
        public string AssetId { get; }
        public bool IsEditingEnabled { get; private set; }
        public bool AllowMultiSelection { get; }

        EditingMode m_EditingMode;
        TextField m_TextField;
        InlineEditBehavior m_InlineEditBehavior;

        IInlineEditService m_InlineEditService;
        VisualElement m_PopupParent;

        EditField m_EditField;
        AssetIdentifier m_AssetIdentifier;

        public event Action<object> EntryEdited;
        public event Func<string, object, bool> IsEntryEdited;

        public EditableTextEntry(string assetId, string title, string details, bool allowSelection = false)
            : base(title, details, allowSelection)
        {
            AssetId = assetId;
            SetupEditField(details);
            ConfigureEditing(EditingMode.ReadOnly);
        }

        public EditableTextEntry(string assetId, string title, string details, bool allowSelection = false, bool allowMultiSelection = false)
            : this(assetId, title, details, allowSelection)
        {
            AllowMultiSelection = allowMultiSelection;
        }

        void SetupEditField(string details)
        {
            m_TextField = new TextField()
            {
                value = details,
            };
            m_TextField.AddToClassList(UssStyle.DetailsPageEntryValue);
            m_TextField.RegisterCallback<KeyUpEvent>(OnKeyUpEvent);
            m_TextField.RegisterCallback<FocusOutEvent>(OnTextFieldFocusOut);
            m_TextField.style.display = DisplayStyle.None;

            UpdateStyling(details);

            hierarchy.Add(m_TextField);
        }

        public void ConfigureEditing(EditingMode mode)
        {
            if (m_EditingMode == mode)
                return;

            m_EditingMode = mode;
            IsEditingEnabled = mode != EditingMode.ReadOnly;

            if (mode == EditingMode.Inline)
                ConfigureInlineMode();
            else
                ConfigureStandardMode();
        }

        void ConfigureInlineMode()
        {
            if (m_InlineEditBehavior == null)
            {
                m_Text.RemoveFromHierarchy();
                m_TextField.RemoveFromHierarchy();
                m_TextField.style.display = DisplayStyle.Flex;

                m_InlineEditBehavior = new InlineEditBehavior(
                    container: this,
                    popupParent: m_PopupParent ?? this,
                    textField: m_TextField,
                    onSave: SaveAsync,
                    onCancel: () => { },
                    editField: m_EditField,
                    onValueSaved: v => m_Text.text = v,
                    inlineEditService: m_InlineEditService,
                    customMetadataType: null);

                m_InlineEditBehavior.Initialize();
            }

            m_InlineEditBehavior.SetEnabled(true);
            style.display = DisplayStyle.Flex;
            UpdateStyling(m_Text.text);
        }

        void ConfigureStandardMode()
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

            m_TextField.value = m_Text.text;
            m_TextField.style.display = IsEditingEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            m_Text.style.display = IsEditingEnabled ? DisplayStyle.None : DisplayStyle.Flex;

            if (!IsEditingEnabled)
                style.display = string.IsNullOrWhiteSpace(m_Text.text) ? DisplayStyle.None : DisplayStyle.Flex;
            else
                UpdateStyling(m_Text.text);
        }

        public void SetEditFieldInfo(AssetIdentifier assetIdentifier, EditField editField, IInlineEditService inlineEditService)
        {
            m_AssetIdentifier = assetIdentifier;
            m_EditField = editField;
            m_InlineEditService = inlineEditService;
        }

        public void SetConfirmationPopupParent(VisualElement parent)
        {
            m_PopupParent = parent;
        }

        async Task SaveAsync(string newValue)
        {
            if (m_InlineEditService == null || m_AssetIdentifier == null)
                throw new InvalidOperationException("Inline editing not properly configured");

            var edit = new AssetFieldEdit(m_AssetIdentifier, m_EditField, newValue);
            var result = await m_InlineEditService.SaveFieldAsync(edit, default);

            if (!result.Success)
                throw new Exception(result.ErrorMessage);
        }

        void OnTextFieldFocusOut(FocusOutEvent _)
        {
            // In inline mode, InlineEditBehavior handles focus-out and calls SaveAsync; we must not
            // update m_Text here or ConfirmEdit will think nothing changed and skip saving.
            if (m_EditingMode == EditingMode.Inline)
                return;
            OnEntryEdited(m_TextField.value);
        }

        void OnKeyUpEvent(KeyUpEvent evt)
        {
            // Only handle keys in non-inline mode (inline mode is handled by InlineEditBehavior)
            if (m_EditingMode == EditingMode.Inline)
                return;

            if (evt.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                OnEntryEdited(m_TextField.value);
        }

        void OnEntryEdited(string newValue)
        {
            if (newValue == m_Text.text)
            {
                return;
            }

            m_Text.text = newValue;
            EntryEdited?.Invoke(newValue);
            UpdateStyling(newValue);
        }

        void UpdateStyling(string value)
        {
            InlineEditStyling.UpdateEditedStyling(
                m_BorderLine,
                m_TextField,
                m_EditingMode,
                () => IsEntryEdited?.Invoke(AssetId, value) ?? false);
        }

        public void SavePendingEdits()
        {
            if (!IsEditingEnabled)
                return;

            OnEntryEdited(m_TextField.value);
        }

        public void Dispose()
        {
            m_InlineEditBehavior?.Cleanup();
            m_InlineEditBehavior = null;
        }
    }
}
