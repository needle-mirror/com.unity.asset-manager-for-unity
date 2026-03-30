using System;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Manages saving-state visual feedback for inline-editable entries.
    /// Subscribes to <see cref="IInlineEditService.SavingStateChanged"/>, swaps the
    /// pencil icon for a loading spinner, and applies the <c>.inline-edit--saving</c>
    /// USS class while any save is in progress.
    /// </summary>
    class InlineEditSavingIndicator : IDisposable
    {
        readonly IInlineEditService m_Service;
        readonly VisualElement m_EditIcon;
        readonly VisualElement m_IconParent;
        readonly VisualElement m_SavingClassTarget;
        readonly Func<bool> m_IsEditing;
        LoadingIcon m_SavingIcon;
        string m_OriginalTooltip;

        public bool IsSaveInProgress => m_Service?.HasAnySaveInProgress == true;

        public InlineEditSavingIndicator(
            IInlineEditService service,
            VisualElement editIcon,
            VisualElement iconParent,
            VisualElement savingClassTarget,
            Func<bool> isEditing)
        {
            m_Service = service;
            m_EditIcon = editIcon;
            m_IconParent = iconParent;
            m_SavingClassTarget = savingClassTarget;
            m_IsEditing = isEditing;

            if (m_Service != null)
            {
                m_Service.SavingStateChanged += OnSavingStateChanged;
                if (m_Service.HasAnySaveInProgress)
                    ShowSavingState();
            }
        }

        void OnSavingStateChanged()
        {
            if (m_IsEditing?.Invoke() == true)
                return;

            if (m_Service?.HasAnySaveInProgress == true)
                ShowSavingState();
            else
                HideSavingState();
        }

        void ShowSavingState()
        {
            if (m_EditIcon != null)
                m_EditIcon.style.display = DisplayStyle.None;

            if (m_SavingIcon == null)
            {
                m_SavingIcon = new LoadingIcon();
                m_SavingIcon.style.width = 14;
                m_SavingIcon.style.height = 14;
                m_SavingIcon.style.marginLeft = StyleKeyword.Auto;
                m_SavingIcon.style.marginRight = 2;
            }

            if (m_SavingIcon.parent == null && m_IconParent != null)
            {
                if (m_EditIcon != null && m_EditIcon.parent == m_IconParent)
                    m_IconParent.Insert(m_IconParent.IndexOf(m_EditIcon), m_SavingIcon);
                else
                    m_IconParent.Insert(Math.Max(0, m_IconParent.childCount - 1), m_SavingIcon);
            }

            m_SavingIcon.style.display = DisplayStyle.Flex;
            m_SavingIcon.PlayAnimation();
            m_SavingClassTarget?.AddToClassList(UssStyle.InlineEditSaving);

            if (m_SavingClassTarget != null)
            {
                m_OriginalTooltip = m_SavingClassTarget.tooltip;
                m_SavingClassTarget.tooltip = "Save in progress…";
            }
        }

        void HideSavingState()
        {
            m_SavingIcon?.StopAnimation();
            m_SavingIcon?.RemoveFromHierarchy();

            if (m_EditIcon != null)
                m_EditIcon.style.display = StyleKeyword.Null;

            m_SavingClassTarget?.RemoveFromClassList(UssStyle.InlineEditSaving);

            if (m_SavingClassTarget != null)
            {
                m_SavingClassTarget.tooltip = m_OriginalTooltip;
                m_OriginalTooltip = null;
            }
        }

        public void Dispose()
        {
            if (m_Service != null)
                m_Service.SavingStateChanged -= OnSavingStateChanged;
            HideSavingState();
        }
    }
}
