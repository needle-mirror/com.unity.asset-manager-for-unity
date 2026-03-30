using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    static partial class UssStyle
    {
        public const string DetailsPageChipField = "details-page-chip-field";
    }

    class EditableListEntry : DetailsPageEntry, IEditableEntry
    {
        List<VisualElement> m_ReadOnlyFields = new();
        List<VisualElement> m_EditFields = new();

        public string AssetId { get; }
        public bool IsEditingEnabled { get; private set; }
        public bool AllowMultiSelection { get; }
        public event Action<object> EntryEdited;
        public event Func<string, object, bool> IsEntryEdited;

        readonly IInlineEditService m_InlineEditService;
        ChipListField m_TagField;
        Func<string, Chip> m_ChipCreator;
        EditingMode m_EditingMode;

        VisualElement m_ValueRow;
        VisualElement m_EditIcon;
        InlineEditStateManager m_StateManager;
        InlineEditConfirmationPopupContainer m_ConfirmationPopup;
        HashSet<string> m_ValuesSnapshotForCancel;

        HashSet<string> m_Values;

        public EditableListEntry(string assetId, string title, IEnumerable<string> values, Func<string, Chip> chipCreator,
            IInlineEditService inlineEditService = null)
            : base(title)
        {
            AssetId = assetId;
            m_Values = values.ToHashSet();
            m_ChipCreator = chipCreator;
            m_InlineEditService = inlineEditService;

            m_ChipContainer = AddChipContainer();
            m_ChipContainer.AddToClassList(UssStyle.FlexWrap);
            m_ReadOnlyFields.Add(m_ChipContainer);

            m_TagField = new ChipListField(m_Values);
            m_TagField.ChipAdded += OnChipAdded;
            m_TagField.ChipRemoved += OnChipRemoved;
            m_TagField.AddToClassList(UssStyle.DetailsPageChipField);
            m_TagField.AddToClassList(UssStyle.DetailsPageEntryValue);
            m_TagField.style.display = DisplayStyle.None;
            hierarchy.Add(m_TagField);

            m_EditFields.Add(m_TagField);

            ConfigureEditing(EditingMode.ReadOnly);
        }

        public EditableListEntry(string assetId, string title, IEnumerable<string> values, Func<string, Chip> chipCreator,
            bool allowMultiSelection, IInlineEditService inlineEditService = null)
            : this(assetId, title, values, chipCreator, inlineEditService)
        {
            AllowMultiSelection = allowMultiSelection;
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
            }
            else
            {
                if (IsEditingEnabled)
                    ToggleEditField();
                else
                    ToggleReadonlyField();
            }
        }

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
            m_ConfirmationPopup?.Dispose();
            m_ConfirmationPopup = new InlineEditConfirmationPopupContainer();
            parent.Add(m_ConfirmationPopup);
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
            m_ValuesSnapshotForCancel = new HashSet<string>(m_Values);
            m_ChipContainer.style.display = DisplayStyle.None;
            m_TagField.style.display = DisplayStyle.Flex;
            m_TagField.UpdateChips(m_Values);
            m_TagField.Focus();

            m_ConfirmationPopup.Show(m_ValueRow, SubmitTagEdits, CancelTagEdits, InlineEditConfirmMode.ButtonOnly);
        }

        void SubmitTagEdits()
        {
            EntryEdited?.Invoke(m_Values);
            ExitTagFieldEditing();
        }

        void CancelTagEdits()
        {
            m_Values.Clear();
            foreach (var v in m_ValuesSnapshotForCancel)
                m_Values.Add(v);
            ExitTagFieldEditing();
        }

        void ExitTagFieldEditing()
        {
            m_ConfirmationPopup?.Hide();
            m_StateManager.ExitEditMode();
            m_ChipContainer.style.display = DisplayStyle.Flex;
            m_TagField.style.display = DisplayStyle.None;
            UpdateReadonlyChipContainer();
            UpdateStyling();
        }

        void ToggleEditField()
        {
            m_EditFields.ForEach(x => x.style.display = DisplayStyle.Flex);
            m_ReadOnlyFields.ForEach(x => x.style.display = DisplayStyle.None);

            UpdateEditableChipContainer();
            UpdateStyling();
        }

        void ToggleReadonlyField()
        {
            m_EditFields.ForEach(x => x.style.display = DisplayStyle.None);
            m_ReadOnlyFields.ForEach(x => x.style.display = DisplayStyle.Flex);

            UpdateReadonlyChipContainer();
            UpdateStyling();
        }

        void UpdateEditableChipContainer()
            => m_TagField.UpdateChips(m_Values);

        void UpdateReadonlyChipContainer()
        {
            m_ChipContainer.Clear();
            foreach (var value in m_Values)
            {
                var chip = m_ChipCreator.Invoke(value);
                if (chip != null)
                    m_ChipContainer.Add(chip);
            }
        }

        void OnChipAdded(string chip)
        {
            m_Values.Add(chip);
            if (!(m_StateManager?.IsEditing ?? false))
                EntryEdited?.Invoke(m_Values);
        }

        void OnChipRemoved(string chip)
        {
            m_Values.Remove(chip);
            if (!(m_StateManager?.IsEditing ?? false))
                EntryEdited?.Invoke(m_Values);
        }

        void UpdateStyling()
        {
            InlineEditStyling.UpdateEditedStyling(
                m_BorderLine,
                m_TagField,
                m_EditingMode,
                () => IsEntryEdited?.Invoke(AssetId, m_Values) ?? false);
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
