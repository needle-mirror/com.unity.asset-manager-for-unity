using System;
using System.Collections.Generic;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class SidebarContent : VisualElement
    {
        const string k_UnityListViewItemSelected = "unity-list-view__item--selected";

        readonly IStateManager m_StateManager;
        readonly SidebarProjectLibraryFoldoutViewModel m_ProjectsFoldoutViewModel;
        readonly SidebarProjectLibraryFoldoutViewModel m_LibrariesFoldoutViewModel;
        readonly ScrollView m_ScrollContainer;
        readonly SidebarViewModel m_ViewModel;

        readonly VisualElement m_AllAssetsButton;
        readonly SidebarSavedViewFoldout m_SidebarSavedViewFoldout;
        readonly SidebarProjectLibraryFoldout m_SidebarProjectFoldout;
        readonly SidebarProjectLibraryFoldout m_SidebarAssetLibraryFoldout;
        readonly VisualElement m_NoProjectsContainer;

        public SidebarContent(SidebarViewModel viewModel, IStateManager stateManager, IMessageManager messageManager)
        {
            m_ViewModel = viewModel;

            m_StateManager = stateManager;

            m_ScrollContainer = new ScrollView
            {
                name = Constants.CategoriesScrollViewUssName,
                mode = ScrollViewMode.Vertical
            };

            m_AllAssetsButton = CreateAllAssetsButton();
            m_ScrollContainer.Add(m_AllAssetsButton);

            var savedViewContentViewModel = m_ViewModel.CreateSavedViewsFoldoutViewModel();
            m_SidebarSavedViewFoldout = new SidebarSavedViewFoldout(savedViewContentViewModel);
            m_ScrollContainer.Add(m_SidebarSavedViewFoldout);

            m_ProjectsFoldoutViewModel = m_ViewModel.CreateProjectLibraryFoldoutViewModel(false);
            m_SidebarProjectFoldout = new SidebarProjectLibraryFoldout( m_ProjectsFoldoutViewModel, m_StateManager, messageManager);
            m_ScrollContainer.Add(m_SidebarProjectFoldout);

            m_LibrariesFoldoutViewModel = m_ViewModel.CreateProjectLibraryFoldoutViewModel(true);
            m_SidebarAssetLibraryFoldout = new SidebarProjectLibraryFoldout(m_LibrariesFoldoutViewModel, m_StateManager, messageManager);
            m_ScrollContainer.Add(m_SidebarAssetLibraryFoldout);

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
            m_ViewModel.BindEvents();

            m_ViewModel.CloudServicesReachabilityChanged += Refresh;
            m_ViewModel.OrganizationChanged += OnOrganizationChanged;
            m_ViewModel.ActivePageChanged += OnActivePageChanged;
            m_ViewModel.ProjectSelectionChanged += OnProjectSelectionChanged;

            Refresh();
            ScrollToHeight(m_StateManager.SideBarScrollValue);
            m_ProjectsFoldoutViewModel.Enabled = true;
            m_LibrariesFoldoutViewModel.Enabled = true;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_ViewModel.UnbindEvents();

            m_ViewModel.CloudServicesReachabilityChanged -= Refresh;
            m_ViewModel.OrganizationChanged -= OnOrganizationChanged;
            m_ViewModel.ActivePageChanged -= OnActivePageChanged;
            m_ViewModel.ProjectSelectionChanged -= OnProjectSelectionChanged;

            m_ProjectsFoldoutViewModel.Enabled = false;
            m_LibrariesFoldoutViewModel.Enabled = false;

            m_StateManager.SideBarScrollValue = m_ScrollContainer.verticalScroller.value;
            m_StateManager.SideBarWidth = layout.width;
        }

        VisualElement CreateAllAssetsButton()
        {
            var allAssetsButton = new VisualElement(){name = "SidebarAllAssetsButton"};
            allAssetsButton.Insert(0, new Image {pickingMode = PickingMode.Ignore, style = {flexShrink = 0}});
            allAssetsButton.Insert(1, new ToolbarSpacer {pickingMode = PickingMode.Ignore, style = {flexShrink = 0}});
            allAssetsButton.Insert(2,new Label(Constants.AllAssetsFolderName));

            allAssetsButton.AddToClassList("allAssetsFolder");

            allAssetsButton.RegisterCallback<PointerDownEvent>(e =>
            {
                m_ViewModel.SelectAllAssetPage();
            }, TrickleDown.TrickleDown);

            return allAssetsButton;
        }

        void OnOrganizationChanged(OrganizationInfo organization)
        {
            Refresh();

            m_SidebarSavedViewFoldout.Refresh();
        }

        void OnProjectSelectionChanged(ProjectOrLibraryInfo projectOrLibraryInfo, CollectionInfo collectionInfo)
        {
            if (projectOrLibraryInfo == null || m_ViewModel.GetActivePage() is AllAssetsPage or AllAssetsInProjectPage)
            {
                m_SidebarProjectFoldout.ClearSelectedProject();
                m_SidebarAssetLibraryFoldout.ClearSelectedProject();
            }
            else
            {
                if (projectOrLibraryInfo.IsAssetLibrary)
                {
                    m_SidebarAssetLibraryFoldout.SelectProject(projectOrLibraryInfo, collectionInfo);
                    m_SidebarSavedViewFoldout.Unselect();
                }
                else
                {
                    m_SidebarProjectFoldout.SelectProject(projectOrLibraryInfo, collectionInfo);
                }
            }
        }

        void OnActivePageChanged(IPage page)
        {
            m_AllAssetsButton.SetEnabled(page is not UploadPage);
            m_AllAssetsButton.EnableInClassList(k_UnityListViewItemSelected, page is AllAssetsPage or AllAssetsInProjectPage);

            m_SidebarAssetLibraryFoldout.SetEnabled(page is CollectionPage or AllAssetsPage);
            m_SidebarSavedViewFoldout.SetEnabled(!m_ViewModel.IsCurrentlySelectingAnAssetLibrary());
            m_SidebarSavedViewFoldout.SetDisplay(page is BasePage { DisplaySavedViewControls: true });
        }

        void Refresh()
        {
            var showAllAssetsFolder = m_ViewModel.GetSelectedOrganization()?.ProjectInfos.Count > 1;
            UIElementsUtils.SetDisplay(m_AllAssetsButton, showAllAssetsFolder);

            m_SidebarProjectFoldout.Refresh();
            m_SidebarAssetLibraryFoldout.Refresh();

            if (!m_ViewModel.AreCloudServicesReachable)
            {
                UIElementsUtils.Hide(m_ScrollContainer);
                UIElementsUtils.Show(m_NoProjectsContainer);
                return;
            }

            var projectInfos = m_ViewModel.GetSelectedOrganization()?.ProjectInfos as IList<ProjectOrLibraryInfo> ??
                               Array.Empty<ProjectOrLibraryInfo>();
            if (projectInfos.Count == 0)
            {
                UIElementsUtils.Hide(m_SidebarProjectFoldout);
                UIElementsUtils.Hide(m_SidebarSavedViewFoldout);
                UIElementsUtils.Show(m_NoProjectsContainer);
                return;
            }

            UIElementsUtils.Show(m_ScrollContainer);
            UIElementsUtils.Show(m_SidebarProjectFoldout);
            UIElementsUtils.Show(m_SidebarSavedViewFoldout);
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

        public void ScrollToElement(VisualElement element)
        {
            if (m_ScrollContainer != null && element != null)
            {
                void OnGeometryChanged(GeometryChangedEvent evt)
                {
                    m_ScrollContainer.ScrollTo(element);
                    element.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                }
                element.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            }
        }
    }
}
