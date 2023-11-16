using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class SideBarButton : Button
    {
        private const string k_UnityListViewItem = "unity-list-view__item";
        private const string k_UnityListViewItemSelected = k_UnityListViewItem + "--selected";

        private string m_CollectionPath;
        private PageType m_PageType;

        private readonly IPageManager m_PageManager;
        public SideBarButton(IPageManager pageManager, string collectionPath, string label, Texture icon, PageType pageType)
        {
            m_PageManager = pageManager;
            m_CollectionPath = collectionPath;
            m_PageType = pageType;

            focusable = true;
            clickable.clicked += () => m_PageManager.activePage = m_PageManager.GetPage(m_PageType, m_CollectionPath);

            AddToClassList(k_UnityListViewItem);
            RemoveFromClassList("unity-button");

            Add(new ToolbarSpacer());
            Add(new Image { image = icon });
            Add(new ToolbarSpacer());
            Add(new Label(label));

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            Refresh(m_PageManager.activePage);
            m_PageManager.onActivePageChanged += Refresh;
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.onActivePageChanged -= Refresh;
        }

        void Refresh(IPage page)
        {
            var selected = page != null && m_PageType == page.pageType && (m_CollectionPath ?? string.Empty) == (page.collectionPath ?? string.Empty);
            EnableInClassList(k_UnityListViewItemSelected, selected);
        }
    }
}