using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class SideBarButton<T> : Button where T : IPage
    {
        private const string k_UnityListViewItem = "unity-list-view__item";
        private const string k_UnityListViewItemSelected = k_UnityListViewItem + "--selected";

        private readonly IPageManager m_PageManager;
        public SideBarButton(IPageManager pageManager, string label, Texture icon)
        {
            m_PageManager = pageManager;

            focusable = true;
            clickable.clicked += () => m_PageManager.SetActivePage<T>();

            AddToClassList(k_UnityListViewItem);
            RemoveFromClassList("unity-button");

            Add(new ToolbarSpacer());
            Add(new Image { image = icon });
            Add(new ToolbarSpacer());
            Add(new Label(label));

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            Refresh(m_PageManager.activePage);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.onActivePageChanged += Refresh;
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.onActivePageChanged -= Refresh;
        }

        void Refresh(IPage page)
        {
            var selected = page is T;
            EnableInClassList(k_UnityListViewItemSelected, selected);
        }
    }
}