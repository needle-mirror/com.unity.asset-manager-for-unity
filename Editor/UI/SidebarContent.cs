using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class SidebarContent : VisualElement
    {
        readonly IPageManager m_PageManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IStateManager m_StateManager;

        SideBarAllAssetsFoldout m_AllAssetsFolder;
        List<SideBarFoldout> m_Foldouts;
        VisualElement m_NoProjectSelectedContainer;
        ScrollView m_ScrollContainer;

        public SidebarContent(IProjectOrganizationProvider projectOrganizationProvider, IPageManager pageManager,
            IStateManager stateManager)
        {
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

            m_AllAssetsFolder = new SideBarAllAssetsFoldout(m_PageManager, m_StateManager,
                m_ProjectOrganizationProvider, Constants.AllAssetsFolderName);
            m_AllAssetsFolder.AddToClassList("allAssetsFolder");
            m_ScrollContainer.Add(m_AllAssetsFolder);
            m_Foldouts = new List<SideBarFoldout> { m_AllAssetsFolder };

            m_NoProjectSelectedContainer = new VisualElement();
            m_NoProjectSelectedContainer.AddToClassList("NoProjectSelected");
            m_NoProjectSelectedContainer.Add(new Label { text = L10n.Tr("No project selected") });

            Add(m_ScrollContainer);
            Add(m_NoProjectSelectedContainer);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_ProjectOrganizationProvider.OrganizationChanged += OrganizationChanged;

            Refresh();
            ScrollToHeight(m_StateManager.SideBarScrollValue);
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_ProjectOrganizationProvider.OrganizationChanged -= OrganizationChanged;

            m_StateManager.SideBarScrollValue = m_ScrollContainer.verticalScroller.value;
            m_StateManager.SideBarWidth = layout.width;
        }

        void OrganizationChanged(OrganizationInfo organization)
        {
            Refresh();
        }

        internal void Refresh()
        {
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
            m_Foldouts.Clear();

            if (projectInfos?.Any() != true)
                return;

            if (projectInfos.Count > 1)
            {
                m_ScrollContainer.Add(m_AllAssetsFolder);
                m_Foldouts.Add(m_AllAssetsFolder);
            }

            foreach (var projectInfo in projectInfos)
            {
                var projectFoldout = new SideBarCollectionFoldout(m_PageManager, m_StateManager,
                    m_ProjectOrganizationProvider, projectInfo.Name, projectInfo, null);
                m_ScrollContainer.Add(projectFoldout);
                m_Foldouts.Add(projectFoldout);

                if (projectInfo.CollectionInfos?.Any() != true)
                    continue;

                projectFoldout.ChangeIntoParentFolder();
                projectFoldout.value = false;
                foreach (var collection in projectInfo.CollectionInfos)
                {
                    if (string.IsNullOrEmpty(collection.ParentPath))
                    {
                        var collectionFoldout =
                            CreateSideBarCollectionFoldout(collection.Name, projectInfo, collection.GetFullPath());
                        m_Foldouts.Add(collectionFoldout);
                        projectFoldout.Add(collectionFoldout);
                    }
                    else
                    {
                        var parentFoldout = GetParentCollectionFoldout(collection);
                        if (parentFoldout == null)
                            continue;

                        var collectionFoldout =
                            CreateSideBarCollectionFoldout(collection.Name, projectInfo, collection.GetFullPath());
                        parentFoldout.ChangeIntoParentFolder();
                        parentFoldout.Add(collectionFoldout);
                        m_Foldouts.Add(collectionFoldout);
                    }
                }
            }
        }

        SideBarFoldout CreateSideBarCollectionFoldout(string foldoutName, ProjectInfo projectInfo,
            string collectionPath)
        {
            return new SideBarCollectionFoldout(m_PageManager, m_StateManager, m_ProjectOrganizationProvider,
                foldoutName, projectInfo, collectionPath);
        }

        SideBarFoldout GetParentCollectionFoldout(CollectionInfo collectionInfo)
        {
            var collectionFoldouts = m_Foldouts.OfType<SideBarCollectionFoldout>();
            return collectionFoldouts.FirstOrDefault(i => i.CollectionId == $"{collectionInfo.ProjectId}/{collectionInfo.ParentPath}");
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
