using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class SidebarContent : VisualElement
    {
        private ScrollView m_ScrollContainer;
        private List<SideBarFoldout> m_Foldouts;
        private SideBarAllAssetsFoldout m_AllAssetsFolder;
        private VisualElement m_NoProjectSelectedContainer;

        private readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        private readonly IPageManager m_PageManager;
        private readonly IStateManager m_StateManager;
        public SidebarContent(IProjectOrganizationProvider projectOrganizationProvider, IPageManager pageManager, IStateManager stateManager)
        {
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_PageManager = pageManager;
            m_StateManager = stateManager;

            InitializeLayout();

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        private void InitializeLayout()
        {
            m_ScrollContainer = new ScrollView
            {
                name = Constants.CategoriesScrollViewUssName,
                mode = ScrollViewMode.Vertical
            };

            m_AllAssetsFolder = new SideBarAllAssetsFoldout(m_PageManager, m_StateManager, m_ProjectOrganizationProvider, Constants.AllAssetsFolderName);
            m_AllAssetsFolder.AddToClassList("allAssetsFolder");
            m_ScrollContainer.Add(m_AllAssetsFolder);
            m_Foldouts = new List<SideBarFoldout> { m_AllAssetsFolder };

            m_NoProjectSelectedContainer = new VisualElement();
            m_NoProjectSelectedContainer.AddToClassList("NoProjectSelected");
            m_NoProjectSelectedContainer.Add(new Label { text = L10n.Tr("No project selected") });

            Add(m_ScrollContainer);
            Add(m_NoProjectSelectedContainer);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_ProjectOrganizationProvider.onOrganizationInfoOrLoadingChanged += OnOrganizationInfoOrLoadingChanged;
            m_ProjectOrganizationProvider.onProjectInfoOrLoadingChanged += OnProjectInfoOrLoadingChanged;

            Refresh();
            ScrollToHeight(m_StateManager.sideBarScrollValue);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_ProjectOrganizationProvider.onOrganizationInfoOrLoadingChanged -= OnOrganizationInfoOrLoadingChanged;
            m_ProjectOrganizationProvider.onProjectInfoOrLoadingChanged -= OnProjectInfoOrLoadingChanged;

            m_StateManager.sideBarScrollValue = m_ScrollContainer.verticalScroller.value;
            m_StateManager.sideBarWidth = layout.width;
        }

        private void OnOrganizationInfoOrLoadingChanged(OrganizationInfo organization, bool isLoading)
        {
            Refresh();
        }

        private void OnProjectInfoOrLoadingChanged(ProjectInfo projectInfo, bool isLoading)
        {
            Refresh();
        }

        internal void Refresh()
        {
            var projectInfos = m_ProjectOrganizationProvider.organization?.projectInfos as IList<ProjectInfo> ?? Array.Empty<ProjectInfo>();
            if (projectInfos.Count == 0)
            {
                UIElementsUtils.Hide(m_ScrollContainer);
                UIElementsUtils.Show(m_NoProjectSelectedContainer);
                return;
            }
            UIElementsUtils.Show(m_ScrollContainer);
            UIElementsUtils.Hide(m_NoProjectSelectedContainer);

            if (m_ProjectOrganizationProvider.organization != null)
            {
                var orderedProjectInfos = m_ProjectOrganizationProvider.organization.projectInfos.OrderBy(p => p.name);
                RebuildProjectList(orderedProjectInfos.ToList());
            }
            else
            {
                RebuildProjectList(null);
            }

            m_ScrollContainer.verticalScroller.value = m_StateManager.sideBarScrollValue;
        }

        private void RebuildProjectList(List<ProjectInfo> projectInfos)
        {
            m_ScrollContainer.Clear();
            m_Foldouts.Clear();

            if (projectInfos?.Any() != true)
                return;

            if (projectInfos.Count > 1)
            {
                m_ScrollContainer.Add(m_AllAssetsFolder);
                m_Foldouts.Add(m_AllAssetsFolder);
                var separator = new HorizontalSeparator();
                var color = separator.style.backgroundColor;
                separator.style.backgroundColor = new Color(color.value.r, color.value.g, color.value.b, 0.25f);
                separator.style.marginBottom = separator.style.marginLeft = separator.style.marginRight = 4;
                m_ScrollContainer.Add(separator);
            }

            foreach (var projectInfo in projectInfos)
            {
                var projectFoldout = new SideBarProjectFoldout(m_PageManager, m_StateManager, m_ProjectOrganizationProvider, projectInfo.name, projectInfo);
                m_ScrollContainer.Add(projectFoldout);
                m_Foldouts.Add(projectFoldout);
                if (projectInfo.collectionInfos?.Any() == true)
                {
                    projectFoldout.ChangeIntoParentFolder();
                    projectFoldout.value = false;
                    foreach (var collection in projectInfo.collectionInfos)
                    {
                        if (string.IsNullOrEmpty(collection.parentPath))
                        {
                            var collectionFoldout = CreateSideBarCollectionFoldout(collection.name, collection.GetFullPath());
                            m_Foldouts.Add(collectionFoldout);
                            projectFoldout.Add(collectionFoldout);
                        }
                        else
                        {
                            var parentFoldout = GetCollectionFoldout(collection.parentPath);
                            if (parentFoldout == null)
                                continue;

                            var collectionFoldout = CreateSideBarCollectionFoldout(collection.name, collection.GetFullPath());
                            parentFoldout.ChangeIntoParentFolder();
                            parentFoldout.Add(collectionFoldout);
                            m_Foldouts.Add(collectionFoldout);
                        }
                    }
                }
            }
        }

        private SideBarFoldout CreateSideBarCollectionFoldout(string foldoutName, string collectionPath)
        {
            return new SideBarCollectionFoldout(m_PageManager, m_StateManager, m_ProjectOrganizationProvider, foldoutName, collectionPath);
        }

        private SideBarFoldout GetCollectionFoldout(string collectionPath)
        {
            var collectionFoldouts = m_Foldouts.OfType<SideBarCollectionFoldout>();
            return collectionFoldouts.FirstOrDefault(i => i.CollectionPath == collectionPath);
        }

        private void ScrollToHeight(float height)
        {
            if (m_ScrollContainer != null)
                m_ScrollContainer.verticalScroller.value = height;
        }
    }
}
