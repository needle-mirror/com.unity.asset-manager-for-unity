using System;
using Unity.AssetManager.Core.Editor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Manages inline edit state, CSS class transitions, and saving-state visual
    /// feedback for editable entries. When an <see cref="IInlineEditService"/> is
    /// provided, the manager owns an <see cref="InlineEditSavingIndicator"/> that
    /// swaps the pencil icon for a loading spinner while saves are in progress.
    /// </summary>
    class InlineEditStateManager : IDisposable
    {
        readonly VisualElement m_Container;
        readonly VisualElement m_ValueRow;
        readonly VisualElement m_EditIcon;
        readonly string m_ContainerEditingClass;
        InlineEditSavingIndicator m_SavingIndicator;

        /// <summary>
        /// Whether the entry is currently in edit mode.
        /// </summary>
        public bool IsEditing { get; private set; }

        /// <summary>
        /// Whether any save operation is currently in progress (delegates to the saving indicator).
        /// </summary>
        public bool IsSaveInProgress => m_SavingIndicator?.IsSaveInProgress == true;

        /// <summary>
        /// Creates a new state manager.
        /// </summary>
        /// <param name="container">The main container element (receives the editing class and saving class).</param>
        /// <param name="valueRow">The value row element (receives InlineEditableEditing class; used as icon parent for the saving indicator).</param>
        /// <param name="editIcon">The edit icon element (hidden during editing and replaced by spinner during saves).</param>
        /// <param name="containerEditingClass">The USS class to apply to container during editing.</param>
        /// <param name="inlineEditService">Optional service for save-in-progress tracking. When provided, a saving indicator is created.</param>
        public InlineEditStateManager(
            VisualElement container,
            VisualElement valueRow,
            VisualElement editIcon,
            string containerEditingClass = null,
            IInlineEditService inlineEditService = null)
        {
            m_Container = container ?? throw new ArgumentNullException(nameof(container));
            m_ValueRow = valueRow;
            m_EditIcon = editIcon;
            m_ContainerEditingClass = containerEditingClass ?? UssStyle.DetailsPageEntryEditing;

            if (inlineEditService != null)
            {
                m_SavingIndicator = new InlineEditSavingIndicator(
                    inlineEditService, editIcon, valueRow, container, () => IsEditing);
            }
        }

        /// <summary>
        /// Enters edit mode: applies editing CSS classes and hides the edit icon.
        /// </summary>
        /// <returns>True if edit mode was entered; false if already editing.</returns>
        public bool EnterEditMode()
        {
            if (IsEditing)
                return false;

            IsEditing = true;
            ApplyEditingStyles();
            return true;
        }

        /// <summary>
        /// Exits edit mode: removes editing CSS classes and shows the edit icon.
        /// </summary>
        /// <returns>True if edit mode was exited; false if not editing.</returns>
        public bool ExitEditMode()
        {
            if (!IsEditing)
                return false;

            IsEditing = false;
            RemoveEditingStyles();
            return true;
        }

        /// <summary>
        /// Applies the editing CSS classes to container, value row, and hides the edit icon.
        /// </summary>
        public void ApplyEditingStyles()
        {
            m_Container.AddToClassList(m_ContainerEditingClass);
            m_ValueRow?.AddToClassList(UssStyle.InlineEditableEditing);

            if (m_EditIcon != null)
                m_EditIcon.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Removes the editing CSS classes from container, value row, and shows the edit icon.
        /// </summary>
        public void RemoveEditingStyles()
        {
            m_Container.RemoveFromClassList(m_ContainerEditingClass);
            m_ValueRow?.RemoveFromClassList(UssStyle.InlineEditableEditing);

            if (m_EditIcon != null)
                m_EditIcon.style.display = StyleKeyword.Null;
        }

        /// <summary>
        /// Checks if an edit can be started (not already editing and no save in progress).
        /// </summary>
        public bool CanStartEdit() => !IsEditing && !IsSaveInProgress;

        public void Dispose()
        {
            m_SavingIndicator?.Dispose();
            m_SavingIndicator = null;
        }
    }
}
