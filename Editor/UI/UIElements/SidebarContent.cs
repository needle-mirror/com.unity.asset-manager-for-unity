using System;
using System.Collections.Generic;
using System.Linq;
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

        readonly Dictionary<string, SideBarCollectionFoldout> m_SideBarProjectFoldouts = new ();

        SideBarAllAssetsFoldout m_AllAssetsFolder;
        VisualElement m_NoProjectSelectedContainer;
        ScrollView m_ScrollContainer;

        public SidebarContent(IUnityConnectProxy unityConnectProxy, IProjectOrganizationProvider projectOrganizationProvider, IPageManager pageManager,
            IStateManager stateManager)
        {
            m_UnityConnectProxy = unityConnectProxy;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_PageManager = pageManager;
            m_StateManager = stateManager;

            InitializeLayout();

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        void InitializeLayout()
        {
            m_ScrollContainer = new ScrollView
            {
                name = Constants.CategoriesScrollViewUssName,
                mode = ScrollViewMode.Vertical
            };

            m_AllAssetsFolder = new SideBarAllAssetsFoldout(m_UnityConnectProxy, m_PageManager, m_StateManager,
                m_ProjectOrganizationProvider, Constants.AllAssetsFolderName);
            m_AllAssetsFolder.AddToClassList("allAssetsFolder");
            m_ScrollContainer.Add(m_AllAssetsFolder);

            m_NoProjectSelectedContainer = new VisualElement();
            m_NoProjectSelectedContainer.AddToClassList("NoProjectSelected");
            m_NoProjectSelectedContainer.Add(new Label {text = L10n.Tr("No project selected")});

            Add(m_ScrollContainer);
            Add(m_NoProjectSelectedContainer);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_UnityConnectProxy.CloudServicesReachabilityChanged += OnCloudServicesReachabilityChanged;
            m_ProjectOrganizationProvider.OrganizationChanged += OnOrganizationChanged;
            m_ProjectOrganizationProvider.ProjectInfoChanged += OnProjectInfoChanged;

            Refresh();
            ScrollToHeight(m_StateManager.SideBarScrollValue);
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_UnityConnectProxy.CloudServicesReachabilityChanged -= OnCloudServicesReachabilityChanged;
            m_ProjectOrganizationProvider.OrganizationChanged -= OnOrganizationChanged;
            m_ProjectOrganizationProvider.ProjectInfoChanged -= OnProjectInfoChanged;

            m_StateManager.SideBarScrollValue = m_ScrollContainer.verticalScroller.value;
            m_StateManager.SideBarWidth = layout.width;
        }

        void OnCloudServicesReachabilityChanged(bool cloudServicesReachable)
        {
            Refresh();
        }

        void OnOrganizationChanged(OrganizationInfo organization)
        {
            Refresh();
        }

        void OnProjectInfoChanged(ProjectInfo projectInfo)
        {
            TryAddCollections(projectInfo);
        }

        void Refresh()
        {
            if (!m_UnityConnectProxy.AreCloudServicesReachable)
            {
                UIElementsUtils.Hide(m_ScrollContainer);
                UIElementsUtils.Show(m_NoProjectSelectedContainer);
                return;
            }

            var projectInfos = m_ProjectOrganizationProvider.SelectedOrganization?.ProjectInfos as IList<ProjectInfo> ??
                Array.Empty<ProjectInfo>();
            if (projectInfos.Count == 0)
            {
                UIElementsUtils.Hide(m_ScrollContainer);
                UIElementsUtils.Show(m_NoProjectSelectedContainer);
                return;
            }

            UIElementsUtils.Show(m_ScrollContainer);
            UIElementsUtils.Hide(m_NoProjectSelectedContainer);

            if (m_ProjectOrganizationProvider.SelectedOrganization != null)
            {
                var orderedProjectInfos =
                    m_ProjectOrganizationProvider.SelectedOrganization.ProjectInfos.OrderBy(p => p.Name);
                RebuildProjectList(orderedProjectInfos.ToList());
            }
            else
            {
                RebuildProjectList(null);
            }

            m_ScrollContainer.verticalScroller.value = m_StateManager.SideBarScrollValue;
        }

        void RebuildProjectList(List<ProjectInfo> projectInfos)
        {
            m_ScrollContainer.Clear();
            m_SideBarProjectFoldouts.Clear();

            if (projectInfos?.Any() != true)
                return;

            if (projectInfos.Count > 1)
            {
                m_ScrollContainer.Add(m_AllAssetsFolder);
            }

            foreach (var projectInfo in projectInfos)
            {
                var projectFoldout = new SideBarCollectionFoldout(m_UnityConnectProxy, m_PageManager, m_StateManager,
                    m_ProjectOrganizationProvider, projectInfo.Name, projectInfo, null);
                m_ScrollContainer.Add(projectFoldout);
                m_SideBarProjectFoldouts[projectInfo.Id] = projectFoldout;

                TryAddCollections(projectInfo);
            }
        }

        void TryAddCollections(ProjectInfo projectInfo)
        {
            // Clean up any existing collection foldouts for the project
            if (!m_SideBarProjectFoldouts.TryGetValue(projectInfo.Id, out var projectFoldout))
                return;

            projectFoldout.Clear();
            projectFoldout.ChangeBackToChildlessFolder();

            projectInfo.OnCollectionsUpdated -= TryAddCollections;
            if (projectInfo.CollectionInfos == null)
            {
                projectInfo.OnCollectionsUpdated += TryAddCollections;
                return;
            }

            if (!projectInfo.CollectionInfos.Any())
                return;

            projectFoldout.value = false;
            foreach (var collection in projectInfo.CollectionInfos)
            {
                CreateFoldoutForParentsThenItself(collection, projectInfo, projectFoldout);
            }
        }

        void CreateFoldoutForParentsThenItself(CollectionInfo collectionInfo, ProjectInfo projectInfo,
            SideBarFoldout projectFoldout)
        {
            if (GetCollectionFoldout(projectInfo, collectionInfo.GetFullPath()) != null)
                return;

            var collectionFoldout =
                CreateSideBarCollectionFoldout(collectionInfo.Name, projectInfo, collectionInfo.GetFullPath());

            SideBarFoldout parentFoldout = null;
            if (!string.IsNullOrEmpty(collectionInfo.ParentPath))
            {
                var parentCollection = projectInfo.GetCollection(collectionInfo.ParentPath);
                CreateFoldoutForParentsThenItself(parentCollection, projectInfo, projectFoldout);

                parentFoldout = GetCollectionFoldout(projectInfo, parentCollection.GetFullPath());
                Utilities.DevAssert(parentFoldout != null);
            }

            var immediateParent = parentFoldout ?? projectFoldout;
            immediateParent.AddFoldout(collectionFoldout);
        }

        SideBarCollectionFoldout CreateSideBarCollectionFoldout(string foldoutName, ProjectInfo projectInfo,
            string collectionPath)
        {
            return new SideBarCollectionFoldout(m_UnityConnectProxy, m_PageManager, m_StateManager, m_ProjectOrganizationProvider,
                foldoutName, projectInfo, collectionPath);
        }

        SideBarFoldout GetCollectionFoldout(ProjectInfo projectInfo, string collectionPath)
        {
            var collectionId = SideBarCollectionFoldout.GetCollectionId(projectInfo, collectionPath);
            return m_ScrollContainer.Q<SideBarFoldout>(collectionId);
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
