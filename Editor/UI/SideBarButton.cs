using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class SideBarButton<T> : Button where T : IPage
    {
        const string k_UnityListViewItem = "unity-list-view__item";
        const string k_UnityListViewItemSelected = k_UnityListViewItem + "--selected";

        readonly IPageManager m_PageManager;

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

            Refresh(m_PageManager.ActivePage);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.ActivePageChanged += Refresh;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.ActivePageChanged -= Refresh;
        }

        void Refresh(IPage page)
        {
            var selected = page is T;
            EnableInClassList(k_UnityListViewItemSelected, selected);
        }
    }
}
