using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class SideBarFoldout : Foldout
    {
        protected const string k_UnityListViewItemSelected = "unity-list-view__item--selected";
        protected const string k_CheckMarkName = "unity-checkmark";
        protected bool m_HasChild;
        protected Toggle m_Toggle;
        private VisualElement m_CheckMark;

        protected readonly IPageManager m_PageManager;
        protected readonly IStateManager m_StateManager;
        protected readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;

        public SideBarFoldout(IPageManager pageManager, IStateManager stateManager, IProjectOrganizationProvider projectOrganizationProvider, string foldoutName)
        {
            m_PageManager = pageManager;
            m_StateManager = stateManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;

            text = foldoutName;

            m_HasChild = false;
            m_Toggle = this.Q<Toggle>();
            m_Toggle.tooltip = foldoutName;
            m_CheckMark = m_Toggle.Q<VisualElement>(k_CheckMarkName);

            var iconParent = this.Q(className: inputUssClassName);
            iconParent.pickingMode = PickingMode.Ignore;
            iconParent.Insert(1, new ToolbarSpacer { pickingMode = PickingMode.Ignore, style = { flexShrink = 0}});
            iconParent.Insert(1, new Image { pickingMode = PickingMode.Ignore, style = { flexShrink = 0}});

            MakeFolderOnlyOpenOnCheckMarkClick();
            AddToClassList("removed-arrow");

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            OnRefresh(m_PageManager.activePage);
            m_PageManager.onActivePageChanged += OnRefresh;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.onActivePageChanged -= OnRefresh;
        }

        protected virtual void OnRefresh(IPage page) { }

        internal virtual void ChangeIntoParentFolder()
        {
            if (m_HasChild)
                return;

            m_HasChild = true;
            UIElementsUtils.Show(m_CheckMark);
            RemoveFromClassList("removed-arrow");
        }

        internal void ChangeBackToChildlessFolder()
        {
            if (!m_HasChild)
                return;

            m_HasChild = false;
            UIElementsUtils.Hide(m_CheckMark);
            AddToClassList("removed-arrow");
        }

        private void MakeFolderOnlyOpenOnCheckMarkClick()
        {
            var label = m_Toggle.Q<Label>();
            label.pickingMode = PickingMode.Ignore;
            m_Toggle.pickingMode = PickingMode.Ignore;
            m_CheckMark.pickingMode = PickingMode.Position;
            UIElementsUtils.Hide(m_CheckMark);
        }
    }
}
