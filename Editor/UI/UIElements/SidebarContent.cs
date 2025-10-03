using System;
using System.Collections.Generic;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class SidebarContent : VisualElement
    {
        readonly IPageManager m_PageManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IStateManager m_StateManager;
        readonly IUnityConnectProxy m_UnityConnectProxy;
        readonly ISidebarContentEnabler m_ProjectContentEnabler;
        readonly ScrollView m_ScrollContainer;

        readonly SideBarAllAssetsFoldout m_AllAssetsFoldout;
        readonly SidebarSavedViewContent m_SidebarSavedViewContent;
        readonly SidebarProjectContent m_SidebarProjectContent;
        readonly VisualElement m_NoProjectsContainer;

        public SidebarContent(IUnityConnectProxy unityConnectProxy, IProjectOrganizationProvider projectOrganizationProvider, IAssetDataManager assetDataManager, IPageManager pageManager,
            IStateManager stateManager, IMessageManager messageManager, ISavedAssetSearchFilterManager savedAssetSearchFilterManager)
        {
            m_UnityConnectProxy = unityConnectProxy;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_PageManager = pageManager;
            m_StateManager = stateManager;

            m_ScrollContainer = new ScrollView
            {
                name = Constants.CategoriesScrollViewUssName,
                mode = ScrollViewMode.Vertical
            };

            m_AllAssetsFoldout = new SideBarAllAssetsFoldout(m_StateManager, messageManager, m_ProjectOrganizationProvider,
                Constants.AllAssetsFolderName, OnAllAssetsFolderClicked);
            m_ScrollContainer.Add(m_AllAssetsFoldout);

            m_SidebarSavedViewContent = new SidebarSavedViewContent(m_ProjectOrganizationProvider, m_PageManager, savedAssetSearchFilterManager);
            m_ScrollContainer.Add(m_SidebarSavedViewContent);

            m_ProjectContentEnabler = new SidebarProjectContentEnabler(m_PageManager, assetDataManager);
            m_SidebarProjectContent = new SidebarProjectContent(m_ProjectOrganizationProvider, m_ProjectContentEnabler, m_StateManager, messageManager);
            m_ScrollContainer.Add(m_SidebarProjectContent);

            m_NoProjectsContainer = new VisualElement();
            m_NoProjectsContainer.AddToClassList("NoProjectSelected");
            m_NoProjectsContainer.Add(new Label {text = L10n.Tr(Constants.SidebarNoProjectsText)});
            Add(m_NoProjectsContainer);

            Add(m_ScrollContainer);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_UnityConnectProxy.CloudServicesReachabilityChanged += OnCloudServicesReachabilityChanged;
            m_ProjectOrganizationProvider.OrganizationChanged += OnOrganizationChanged;
            m_ProjectOrganizationProvider.ProjectSelectionChanged += OnProjectSelectionChanged;

            Refresh();
            ScrollToHeight(m_StateManager.SideBarScrollValue);
            
            OnActivePageChanged(m_PageManager.ActivePage);
            m_PageManager.ActivePageChanged += OnActivePageChanged;
            
            m_ProjectContentEnabler.Enabled = true;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_UnityConnectProxy.CloudServicesReachabilityChanged -= OnCloudServicesReachabilityChanged;
            m_ProjectOrganizationProvider.OrganizationChanged -= OnOrganizationChanged;
            m_ProjectOrganizationProvider.ProjectSelectionChanged -= OnProjectSelectionChanged;
            m_PageManager.ActivePageChanged -= OnActivePageChanged;

            m_ProjectContentEnabler.Enabled = false;

            m_StateManager.SideBarScrollValue = m_ScrollContainer.verticalScroller.value;
            m_StateManager.SideBarWidth = layout.width;
        }

        void OnCloudServicesReachabilityChanged(bool cloudServicesReachable)
        {
            Refresh();
            
            OnActivePageChanged(m_PageManager.ActivePage);
        }

        void OnOrganizationChanged(OrganizationInfo organization)
        {
            Refresh();

            m_SidebarSavedViewContent.Refresh();
        }

        void OnProjectSelectionChanged(ProjectInfo projectInfo, CollectionInfo collectionInfo)
        {
            if (m_PageManager.ActivePage is AllAssetsPage or AllAssetsInProjectPage)
            {
                m_SidebarProjectContent.ClearSelectedProject();
            }
            else
            {
                m_SidebarProjectContent.SelectProject(projectInfo, collectionInfo);
            }
        }

        void OnActivePageChanged(IPage page)
        {
            m_AllAssetsFoldout.SetEnabled(page is not UploadPage);
            m_AllAssetsFoldout.SetSelected(page is AllAssetsPage or AllAssetsInProjectPage);
            
            m_SidebarSavedViewContent.SetDisplay(page is BasePage {DisplaySavedViewControls: true});
            
            OnProjectSelectionChanged(m_ProjectOrganizationProvider.SelectedProject, m_ProjectOrganizationProvider.SelectedCollection);
        }
        
        void OnAllAssetsFolderClicked()
        {
            m_PageManager.SetActivePage<AllAssetsPage>();
        }

        void Refresh()
        {
            var showAllAssetsFolder = m_ProjectOrganizationProvider.SelectedOrganization?.ProjectInfos.Count > 1;
            UIElementsUtils.SetDisplay(m_AllAssetsFoldout, showAllAssetsFolder);

            m_SidebarProjectContent.Refresh();

            if (!m_UnityConnectProxy.AreCloudServicesReachable)
            {
                UIElementsUtils.Hide(m_ScrollContainer);
                UIElementsUtils.Show(m_NoProjectsContainer);
                return;
            }

            var projectInfos = m_ProjectOrganizationProvider.SelectedOrganization?.ProjectInfos as IList<ProjectInfo> ??
                Array.Empty<ProjectInfo>();
            if (projectInfos.Count == 0)
            {
                UIElementsUtils.Hide(m_ScrollContainer);
                UIElementsUtils.Show(m_NoProjectsContainer);
                return;
            }

            UIElementsUtils.Show(m_ScrollContainer);
            UIElementsUtils.Hide(m_NoProjectsContainer);

            m_ScrollContainer.verticalScroller.value = m_StateManager.SideBarScrollValue;
        }

        void ScrollToHeight(float height)
        {
            if (m_ScrollContainer != null)
            {
                m_ScrollContainer.verticalScroller.value = height;
            }
        }
    }
}
