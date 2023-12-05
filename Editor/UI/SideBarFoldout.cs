using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class SideBarFoldout : Foldout
    {
        private const string k_UnityListViewItemSelected = "unity-list-view__item--selected";
        private const string k_CheckMarkName = "unity-checkmark";
        public string collectionPath { get; }
        private bool m_HasChild;
        private Toggle m_Toggle;
        private VisualElement m_CheckMark;
        private readonly bool m_Selectable;

        private readonly IPageManager m_PageManager;
        private readonly IStateManager m_StateManager;
        public SideBarFoldout(IPageManager pageManager, IStateManager stateManager, string foldoutName, string collectionPath, bool selectable, Texture icon)
        {
            m_PageManager = pageManager;
            m_StateManager = stateManager;

            text = foldoutName;
            this.collectionPath = collectionPath;
            m_HasChild = false;
            m_Toggle = this.Q<Toggle>();
            m_Toggle.tooltip = foldoutName;
            m_CheckMark = m_Toggle.Q<VisualElement>(k_CheckMarkName);

            var iconParent = this.Q(className: inputUssClassName);
            iconParent.pickingMode = PickingMode.Ignore;
            iconParent.Insert(1, new ToolbarSpacer { pickingMode = PickingMode.Ignore });
            iconParent.Insert(1, new Image { image = icon, pickingMode = PickingMode.Ignore });

            MakeFolderOnlyOpenOnCheckMarkClick();
            RegisterEventForIconChange();
            AddToClassList("removed-arrow");

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            m_Selectable = selectable;
            if (!selectable)
                return;

            RegisterCallback<PointerDownEvent>(e =>
            {
                var target = (VisualElement)e.target;
                // We skip the user's click if they aimed the check mark of the foldout
                // to only select foldouts when they click on it's title/label
                if (e.button != 0 || target.name == k_CheckMarkName)
                    return;
                m_PageManager.activePage = m_PageManager.GetPage(PageType.Collection, collectionPath);
            }, TrickleDown.TrickleDown);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            if (!m_Selectable)
                return;
            Refresh(m_PageManager.activePage);
            m_PageManager.onActivePageChanged += Refresh;
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            if (!m_Selectable)
                return;
            m_PageManager.onActivePageChanged -= Refresh;
        }

        void Refresh(IPage page)
        {
            var selected = page != null && page.pageType == PageType.Collection && (collectionPath ?? string.Empty) == (page.collectionPath ?? string.Empty);
            m_Toggle.EnableInClassList(k_UnityListViewItemSelected, selected);
        }

        private void SetIcon()
        {
            if (!m_HasChild)
                return;

            var iconParent = this.Q(className: inputUssClassName);
            var image = iconParent.Q<Image>();
            
            image.image = value
                ? UIElementsUtils.GetCategoryIcon(Constants.CategoriesAndIcons[Constants.OpenFoldoutName])
                : UIElementsUtils.GetCategoryIcon(Constants.CategoriesAndIcons[Constants.ClosedFoldoutName]);
        }

        internal void ChangeIntoParentFolder()
        {
            if (m_HasChild)
                return;

            m_HasChild = true;
            m_CheckMark.style.display =  DisplayStyle.Flex;
            m_CheckMark.style.visibility = Visibility.Visible;
            RemoveFromClassList("removed-arrow");

            if (!string.IsNullOrEmpty(collectionPath))
            {
                value = !m_StateManager.collapsedCollections.Contains(collectionPath);
                SetIcon();
            } 
        }

        internal void ChangeBackToChildlessFolder()
        {
            if (!m_HasChild)
                return;

            m_HasChild = false;
            m_CheckMark.style.display =  DisplayStyle.None;
            m_CheckMark.style.visibility = Visibility.Hidden;
            AddToClassList("removed-arrow");
        }

        private void MakeFolderOnlyOpenOnCheckMarkClick()
        {
            var label = m_Toggle.Q<Label>();
            label.pickingMode = PickingMode.Ignore;
            m_Toggle.pickingMode = PickingMode.Ignore;
            m_CheckMark.pickingMode = PickingMode.Position;
            m_CheckMark.style.display =  DisplayStyle.None;
            m_CheckMark.style.visibility = Visibility.Hidden;
        }

        private void RegisterEventForIconChange()
        {
            this.RegisterValueChangedCallback(e =>
            {
                SetIcon();
                if (m_HasChild)
                    if (!value)
                        m_StateManager.collapsedCollections.Add(collectionPath);
                    else
                        m_StateManager.collapsedCollections.Remove(collectionPath);
            });
        }
    }
}
