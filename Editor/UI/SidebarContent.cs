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
        private SideBarFoldout m_CollectionsFolder;
        private SideBarFoldout m_AllAssetsFolder;
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

            var allAssetsIcon = UIElementsUtils.GetCategoryIcon(Constants.CategoriesAndIcons[Constants.AllAssetsFolderName]);
            m_AllAssetsFolder = CreateSideBarFoldout(Constants.AllAssetsFolderName, string.Empty, icon: allAssetsIcon);
            m_AllAssetsFolder.AddToClassList("allAssetsFolder");
            m_ScrollContainer.Add(m_AllAssetsFolder);
            m_CollectionsFolder = CreateSideBarFoldout("Collections", null, false);
            m_CollectionsFolder.RegisterValueChangedCallback(e =>
            {
                m_StateManager.collectionsTopFolderFoldoutValue = m_CollectionsFolder.value;
            });
            m_CollectionsFolder.value = m_StateManager.collectionsTopFolderFoldoutValue;
            m_CollectionsFolder.AddToClassList("collections-top-level-folder");
            m_ScrollContainer.Add(m_CollectionsFolder);
            m_Foldouts = new List<SideBarFoldout> { m_AllAssetsFolder };

            m_NoProjectSelectedContainer = new VisualElement();
            m_NoProjectSelectedContainer.AddToClassList("NoProjectSelected");
            m_NoProjectSelectedContainer.Add(new Label { text = L10n.Tr("No project selected") });

            Add(m_ScrollContainer);
            Add(m_NoProjectSelectedContainer);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_ProjectOrganizationProvider.onProjectSelectionChanged += OnProjectSelectionChanged;
            m_ProjectOrganizationProvider.onOrganizationInfoOrLoadingChanged += OnOrganizationInfoOrLoadingChanged;

            Refresh();
            ScrollToHeight(m_StateManager.sideBarScrollValue);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_ProjectOrganizationProvider.onProjectSelectionChanged -= OnProjectSelectionChanged;
            m_ProjectOrganizationProvider.onOrganizationInfoOrLoadingChanged += OnOrganizationInfoOrLoadingChanged;

            m_StateManager.sideBarScrollValue = m_ScrollContainer.verticalScroller.value;
            m_StateManager.sideBarWidth = layout.width;
        }

        private void OnProjectSelectionChanged(ProjectInfo project)
        {
            Refresh();
        }

        private void OnOrganizationInfoOrLoadingChanged(OrganizationInfo organization, bool isLoading)
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

            RebuildCollectionList(m_ProjectOrganizationProvider.selectedProject?.collectionInfos);

            m_ScrollContainer.verticalScroller.value = m_StateManager.sideBarScrollValue;
        }

        private void RebuildCollectionList(IReadOnlyCollection<CollectionInfo> collections)
        {
            m_CollectionsFolder.Clear();
            m_Foldouts.Clear();
            m_Foldouts.Add(m_AllAssetsFolder);
            m_CollectionsFolder.ChangeBackToChildlessFolder();
            m_CollectionsFolder.SetEnabled(false);

            if (collections?.Any() != true)
                return;

            m_CollectionsFolder.SetEnabled(true);
            m_CollectionsFolder.ChangeIntoParentFolder();

            foreach (var collection in collections)
            {
                if (string.IsNullOrEmpty(collection.parentPath))
                {
                    var foldout = CreateSideBarFoldout(collection.name, collection.GetFullPath());
                    m_Foldouts.Add(foldout);
                    m_CollectionsFolder.Add(foldout);
                }
                else
                {
                    var parentFoldout = GetFoldout(collection.parentPath);
                    if (parentFoldout == null)
                        continue;
                    var foldout = CreateSideBarFoldout(collection.name, collection.GetFullPath());
                    parentFoldout.ChangeIntoParentFolder();
                    parentFoldout.Add(foldout);
                    m_Foldouts.Add(foldout);
                }
            }
        }

        private SideBarFoldout CreateSideBarFoldout(string foldoutName, string collectionPath, bool selectable = true, Texture icon = null)
        {
            icon ??= UIElementsUtils.GetCategoryIcon(Constants.CategoriesAndIcons[Constants.ClosedFoldoutName]);
            return new SideBarFoldout(m_PageManager, m_StateManager, foldoutName, collectionPath, selectable, icon);
        }

        private SideBarFoldout GetFoldout(string collectionPath)
        {
            return m_Foldouts.FirstOrDefault(i => i.collectionPath == collectionPath);
        }

        private void ScrollToHeight(float height)
        {
            if (m_ScrollContainer != null)
                m_ScrollContainer.verticalScroller.value = height;
        }
    }
}