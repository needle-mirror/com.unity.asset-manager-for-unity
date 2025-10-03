using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class SidebarProjectContent : Foldout
    {
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly ISidebarContentEnabler m_SidebarContentEnabler;
        readonly IStateManager m_StateManager;
        readonly IMessageManager m_MessageManager;

        readonly Dictionary<string, SideBarCollectionFoldout> m_SideBarProjectFoldouts = new();

        public SidebarProjectContent(IProjectOrganizationProvider projectOrganizationProvider, ISidebarContentEnabler sidebarContentEnabler,
            IStateManager stateManager, IMessageManager messageManager)
        {
            var toggle = this.Q<Toggle>();
            toggle.text = Constants.SidebarProjectsText;
            toggle.AddToClassList("SidebarContentTitle");

            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_SidebarContentEnabler = sidebarContentEnabler;
            m_StateManager = stateManager;
            m_MessageManager = messageManager;

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }
        
        void OnAttachToPanel(AttachToPanelEvent _)
        {
            m_ProjectOrganizationProvider.ProjectInfoChanged += ProjectInfoChanged;
            m_SidebarContentEnabler.AllInvalidated += RefreshEnabledStates;
        }
        
        void OnDetachFromPanel(DetachFromPanelEvent _)
        {
            m_ProjectOrganizationProvider.ProjectInfoChanged -= ProjectInfoChanged;
            m_SidebarContentEnabler.AllInvalidated -= RefreshEnabledStates;
        }

        public void SelectProject(ProjectInfo projectInfo, CollectionInfo collectionInfo)
        {
            // First try to select the collection, if not available select the project
            var selectedId = GetCollectionId(collectionInfo);
            selectedId = string.IsNullOrEmpty(selectedId) ? projectInfo?.Id : selectedId;

            foreach (var projectFoldout in m_SideBarProjectFoldouts)
            {
                SetSelectedRecursive(projectFoldout.Value, selectedId);
            }
        }

        public void ClearSelectedProject()
        {
            foreach (var kvp in m_SideBarProjectFoldouts)
            {
                SetSelectedRecursive(kvp.Value, null);
            }
        }

        void ProjectInfoChanged(ProjectInfo projectInfo)
        {
            TryAddCollections(projectInfo);
        }

        async void RefreshEnabledStates()
        {
            try
            {
                var tasks = new List<Task>();

                foreach (var kvp in m_SideBarProjectFoldouts)
                {
                    RefreshEnabledStateRecursive(kvp.Value, tasks);
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception e)
            {
                Utilities.DevLogException(e);
            }
        }

        void RefreshEnabledStateRecursive(SideBarFoldout foldout, List<Task> tasks)
        {
            tasks.Add(RefreshEnabledState(foldout));

            foreach (var child in foldout.Children())
            {
                if (child is SideBarFoldout childFoldout)
                {
                    RefreshEnabledStateRecursive(childFoldout, tasks);
                }
            }
        }

        async Task RefreshEnabledState(SideBarFoldout foldout)
        {
            var isEnabled = await m_SidebarContentEnabler.IsEntryEnabledAsync(foldout.name, CancellationToken.None);
            
            m_StateManager.SetCollectionFoldoutPopulatedState(foldout.name, isEnabled);
            
            foldout.SetPopulatedState(isEnabled);
        }

        public void Refresh()
        {
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
        }

        void RebuildProjectList(List<ProjectInfo> projectInfos)
        {
            foreach (var foldout in m_SideBarProjectFoldouts.Values)
                Remove(foldout);

            m_SideBarProjectFoldouts.Clear();

            if (projectInfos?.Any() != true)
                return;

            foreach (var projectInfo in projectInfos)
            {
                var projectFoldout = new SideBarCollectionFoldout(
                    m_StateManager, m_MessageManager, m_ProjectOrganizationProvider,
                    projectInfo.Name, projectInfo.Id, null,
                    m_StateManager.GetCollectionFoldoutPopulatedState(projectInfo.Id));
                Add(projectFoldout);
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

            if (projectInfo.CollectionInfos == null)
                return;

            if (!projectInfo.CollectionInfos.Any())
                return;

            projectFoldout.value = false;
            var orderedCollectionInfos = projectInfo.CollectionInfos.OrderBy(c => c.GetFullPath());
            foreach (var collection in orderedCollectionInfos)
            {
                CreateFoldoutForParentsThenItself(collection, projectInfo, projectFoldout);
            }
        }

        void CreateFoldoutForParentsThenItself(CollectionInfo collectionInfo, ProjectInfo projectInfo,
            SideBarFoldout projectFoldout)
        {
            var collectionId = GetCollectionId(collectionInfo);
            if (this.Q<SideBarFoldout>(collectionId) != null)
                return;

            var collectionFoldout = new SideBarCollectionFoldout(
                m_StateManager, m_MessageManager, m_ProjectOrganizationProvider,
                collectionInfo.Name, projectInfo.Id, collectionInfo.GetFullPath(),
                m_StateManager.GetCollectionFoldoutPopulatedState(collectionId));
            collectionFoldout.SetSelected(GetCollectionId(m_ProjectOrganizationProvider.SelectedCollection) == collectionId);

            SideBarFoldout parentFoldout = null;
            if (!string.IsNullOrEmpty(collectionInfo.ParentPath))
            {
                var parentCollection = projectInfo.GetCollection(collectionInfo.ParentPath);
                CreateFoldoutForParentsThenItself(parentCollection, projectInfo, projectFoldout);

                parentFoldout = this.Q<SideBarFoldout>(GetCollectionId(parentCollection));
                Utilities.DevAssert(parentFoldout != null);
            }

            var immediateParent = parentFoldout ?? projectFoldout;
            immediateParent.AddFoldout(collectionFoldout);
        }

        static void SetSelectedRecursive(SideBarFoldout foldout, string selectedId)
        {
            foldout.SetSelected(foldout.name == selectedId);
            foreach (var child in foldout.Children())
            {
                if (child is SideBarFoldout childFoldout)
                {
                    SetSelectedRecursive(childFoldout, selectedId);
                }
            }
        }

        static string GetCollectionId(CollectionInfo collectionInfo) =>
            collectionInfo == null
                ? null
                : SideBarCollectionFoldout.GetCollectionId(collectionInfo.ProjectId, collectionInfo.GetFullPath());
    }
}
