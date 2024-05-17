using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class SideBar : VisualElement
    {
        const string k_IsSidebarCollapsedPrefKey = "com.unity.asset-manager-for-unity.isSidebarCollapsed";

        readonly TwoPaneSplitView m_CategoriesSplitView;
        readonly IPageManager m_PageManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly VisualElement m_DraglineAnchor;
        readonly IStateManager m_StateManager;
        readonly IUnityConnectProxy m_UnityConnectProxy;

        VisualElement m_Sidebar;
        VisualElement m_CollapsedSidebar;
        float m_DraglineHorizontalPosition;


        static bool IsCollapsed
        {
            get => EditorPrefs.GetBool(k_IsSidebarCollapsedPrefKey, false);
            set => EditorPrefs.SetBool(k_IsSidebarCollapsedPrefKey, value);
        }

        public SideBar(IUnityConnectProxy unityConnectProxy, IStateManager stateManager, IPageManager pageManager,
            IProjectOrganizationProvider projectOrganizationProvider,
            TwoPaneSplitView categoriesSplitView)
        {
            m_UnityConnectProxy = unityConnectProxy;
            m_StateManager = stateManager;
            m_PageManager = pageManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;

            // We need this to hide/show the draggable line between the panes.
            m_DraglineAnchor = categoriesSplitView.Q("unity-dragline-anchor");
            m_DraglineHorizontalPosition = categoriesSplitView.fixedPaneInitialDimension;

            if (IsCollapsed)
            {
                SwitchToCollapsedSidebar();
            }
            else
            {
                SwitchToExpandedSidebar();
            }
        }

        VisualElement CreateSidebar()
        {
            var sidebar = new VisualElement();
            sidebar.AddToClassList("flex-grow-1");

            var topSection = new VisualElement();
            topSection.AddToClassList("sidebar-top-section");

            var title = new Label("Projects");
            title.AddToClassList("SidebarTitle");

            var button = new Button();
            button.AddToClassList("unity-list-view__item");
            button.AddToClassList("collapse-sidebar-button");
            button.RemoveFromClassList("unity-button");
            button.clickable.clicked += SwitchToCollapsedSidebar;

            topSection.Add(title);
            topSection.Add(button);

            var footerContainer = new VisualElement { name = "FooterContainer" };
            footerContainer.Add(new HorizontalSeparator());
            footerContainer.Add(new SideBarButton<InProjectPage>(m_PageManager, "In Project", "icon-in-project"));

            sidebar.Add(topSection);
            sidebar.Add(new HorizontalSeparator());
            sidebar.Add(new SidebarContent(m_UnityConnectProxy, m_ProjectOrganizationProvider, m_PageManager, m_StateManager));
            sidebar.Add(footerContainer);

            return sidebar;
        }

        VisualElement CreateCollapsedSidebar()
        {
            var collapsedSidebar = new VisualElement();
            collapsedSidebar.AddToClassList("flex-grow-1");

            var button = new Button();
            button.AddToClassList("unity-list-view__item");
            button.AddToClassList("expend-sidebar-button");
            button.RemoveFromClassList("unity-button");
            button.clickable.clicked += SwitchToExpandedSidebar;

            var footerContainer = new VisualElement {name = "FooterContainer"};
            footerContainer.Add(new HorizontalSeparator());
            footerContainer.Add(new SideBarButton<InProjectPage>(m_PageManager, null, "icon-in-project"));

            collapsedSidebar.Add(button);
            if (m_UnityConnectProxy.AreCloudServicesReachable)
            {
                collapsedSidebar.Add(new HorizontalSeparator());
                collapsedSidebar.Add(new SideBarButton<AllAssetsPage>(m_PageManager, null, "icon-all-assets"));
            }
            collapsedSidebar.Add(footerContainer);    

            return collapsedSidebar;
        }

        void SwitchToExpandedSidebar()
        {
            if (m_Sidebar == null)
            {
                m_Sidebar = CreateSidebar();
                Add(m_Sidebar);
            }
            else
            {
                UIElementsUtils.Show(m_Sidebar);
            }

            UIElementsUtils.Hide(m_CollapsedSidebar);
            UIElementsUtils.Show(m_DraglineAnchor);
            m_DraglineAnchor.style.left = new Length(m_DraglineHorizontalPosition, LengthUnit.Pixel);

            AddToClassList("expanded-sidebar");
            RemoveFromClassList("collapsed-sidebar");

            IsCollapsed = false;
        }

        void SwitchToCollapsedSidebar()
        {
            if (!float.IsNaN(resolvedStyle.width))
            {
                m_DraglineHorizontalPosition = resolvedStyle.width;
            }

            if (m_CollapsedSidebar == null)
            {
                m_CollapsedSidebar = CreateCollapsedSidebar();
                Add(m_CollapsedSidebar);
            }
            else
            {
                UIElementsUtils.Show(m_CollapsedSidebar);
            }

            UIElementsUtils.Hide(m_Sidebar);
            UIElementsUtils.Hide(m_DraglineAnchor);

            AddToClassList("collapsed-sidebar");
            RemoveFromClassList("expanded-sidebar");

            IsCollapsed = true;
        }
    }
}
