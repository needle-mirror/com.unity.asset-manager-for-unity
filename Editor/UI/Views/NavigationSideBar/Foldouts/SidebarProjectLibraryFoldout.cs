using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class SidebarProjectLibraryFoldout : Foldout
    {
        readonly SidebarProjectLibraryFoldoutViewModel m_ViewModel;
        readonly IStateManager m_StateManager;
        readonly IMessageManager m_MessageManager;

        readonly Dictionary<string, SidebarCollectionFoldout> m_SidebarProjectFoldouts = new();
        Task m_RefreshTask;

        public SidebarProjectLibraryFoldout(SidebarProjectLibraryFoldoutViewModel viewModel,
            IStateManager stateManager, IMessageManager messageManager)
        {
            m_ViewModel = viewModel;

            var toggle = this.Q<Toggle>();
            toggle.text = m_ViewModel.IsLibraryFoldout ? Constants.SidebarAssetLibrariesText : Constants.SidebarProjectsText;
            toggle.AddToClassList("SidebarContentTitle");
            toggle.focusable = false;

            m_StateManager = stateManager;
            m_MessageManager = messageManager;

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            if (m_ViewModel.IsLibraryFoldout)
            {
                UIElementsUtils.Hide(this);
            }
        }

        void OnAttachToPanel(AttachToPanelEvent _)
        {
            m_ViewModel.BindEvents();
            m_ViewModel.ProjectInfoChanged += ProjectInfoChanged;
            m_ViewModel.AllInvalidated += RefreshEnabledStates;
            m_ViewModel.AssetLibrariesProjectsLoaded += RebuildList;
        }

        void OnDetachFromPanel(DetachFromPanelEvent _)
        {
            m_ViewModel.UnbindEvents();
            m_ViewModel.ProjectInfoChanged -= ProjectInfoChanged;
            m_ViewModel.AllInvalidated -= RefreshEnabledStates;
            m_ViewModel.AssetLibrariesProjectsLoaded -= RebuildList;
        }

        public void SelectProject(ProjectOrLibraryInfo projectOrLibraryInfo, CollectionInfo collectionInfo)
        {
            // First try to select the collection, if not available select the project
            var selectedId = GetCollectionId(collectionInfo);
            selectedId = string.IsNullOrEmpty(selectedId) ? projectOrLibraryInfo?.Id : selectedId;

            foreach (var projectFoldout in m_SidebarProjectFoldouts)
            {
                SetSelectedRecursive(projectFoldout.Value, selectedId);
            }
        }

        public void ClearSelectedProject()
        {
            foreach (var kvp in m_SidebarProjectFoldouts)
            {
                SetSelectedRecursive(kvp.Value, null);
            }
        }

        void ProjectInfoChanged(ProjectOrLibraryInfo projectOrLibraryInfo)
        {
            TryAddCollections(projectOrLibraryInfo);
        }

        async void RefreshEnabledStates()
        {
            try
            {
                var tasks = new List<Task>();

                foreach (var kvp in m_SidebarProjectFoldouts)
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

        void RefreshEnabledStateRecursive(SidebarFoldout foldout, List<Task> tasks)
        {
            tasks.Add(RefreshEnabledState(foldout));

            foreach (var child in foldout.Children())
            {
                if (child is SidebarFoldout childFoldout)
                {
                    RefreshEnabledStateRecursive(childFoldout, tasks);
                }
            }
        }

        Task RefreshEnabledState(SidebarFoldout foldout)
        {
            m_StateManager.SetCollectionFoldoutPopulatedState(foldout.name, true);
            foldout.SetPopulatedState(true);
            return Task.CompletedTask;
        }

        public void Refresh()
        {
            if (m_ViewModel.IsLibraryFoldout)
            {
                RebuildAssetLibrariesListAsync();
            }
            else
            {
                RebuildList(m_ViewModel.GetProjects());
            }
        }

        async void RebuildAssetLibrariesListAsync()
        {
            RebuildList(await m_ViewModel.GetAssetLibrariesAsync());
        }

        void RebuildList(List<ProjectOrLibraryInfo> projectInfos)
        {
            foreach (var foldout in m_SidebarProjectFoldouts.Values)
                Remove(foldout);

            m_SidebarProjectFoldouts.Clear();

            if (projectInfos?.Any() != true)
                return;

            UIElementsUtils.Show(this);

            foreach (var projectInfo in projectInfos)
            {
                var projectFoldout = new SidebarCollectionFoldout(m_ViewModel.GetSidebarCollectionFoldoutViewModel(projectInfo.Id, string.Empty, projectInfo.Name),
                    m_StateManager, m_MessageManager,
                    m_StateManager.GetCollectionFoldoutPopulatedState(projectInfo.Id));
                Add(projectFoldout);
                m_SidebarProjectFoldouts[projectInfo.Id] = projectFoldout;

                TryAddCollections(projectInfo);
            }
        }

        void TryAddCollections(ProjectOrLibraryInfo projectOrLibraryInfo)
        {
            // Clean up any existing collection foldouts for the project
            if (!m_SidebarProjectFoldouts.TryGetValue(projectOrLibraryInfo.Id, out var projectFoldout))
                return;

            // Capture the state of any foldout that is currently in naming or renaming mode
            var namingState = FindNamingState(projectFoldout);

            projectFoldout.Clear();
            projectFoldout.ChangeBackToChildlessFolder();

            if (projectOrLibraryInfo.CollectionInfos == null)
                return;

            if (!projectOrLibraryInfo.CollectionInfos.Any())
                return;

            projectFoldout.value = false;
            var orderedCollectionInfos = projectOrLibraryInfo.CollectionInfos.OrderBy(c => c.GetFullPath());
            foreach (var collection in orderedCollectionInfos)
            {
                CreateFoldoutForParentsThenItself(collection, projectOrLibraryInfo, projectFoldout);
            }

            // Restore naming/renaming state after rebuilding
            if (namingState.HasValue)
            {
                RestoreNamingState(projectFoldout, namingState.Value);
            }
        }

        FoldoutNamingState? FindNamingState(SidebarFoldout foldout)
        {
            if (foldout is SidebarCollectionFoldout collectionFoldout)
            {
                var state = collectionFoldout.GetNamingState();
                if (state.IsInNamingMode)
                {
                    return state;
                }
            }

            foreach (var child in foldout.Children())
            {
                if (child is SidebarFoldout childFoldout)
                {
                    var result = FindNamingState(childFoldout);
                    if (result.HasValue)
                        return result;
                }
            }

            return null;
        }

        void RestoreNamingState(SidebarCollectionFoldout projectFoldout, FoldoutNamingState foldoutNamingState)
        {
            if (foldoutNamingState.IsRenaming)
            {
                // Find existing foldout and restore renaming state
                // Scope the search to the project foldout subtree
                var existingFoldout = projectFoldout.Q<SidebarCollectionFoldout>(foldoutNamingState.CollectionId);
                existingFoldout?.RestoreNamingState(foldoutNamingState);
            }
            else if (foldoutNamingState.IsNaming)
            {
                // Create temporary foldout for naming state
                CreateTemporaryNamingFoldout(projectFoldout, foldoutNamingState);
            }
        }

        void CreateTemporaryNamingFoldout(SidebarCollectionFoldout projectFoldout, FoldoutNamingState foldoutNamingState)
        {
            // We need to recreate the temporary foldout used for naming a new collection

            // Find the parent foldout directly using the stored parent collection ID
            // Scope the search to the project foldout subtree
            var parentFoldout = projectFoldout;
            if (!string.IsNullOrEmpty(foldoutNamingState.ParentCollectionId))
            {
                parentFoldout = projectFoldout.Q<SidebarCollectionFoldout>(foldoutNamingState.ParentCollectionId) ?? projectFoldout;
            }

            // Create the temporary foldout
            var tempFoldout = new SidebarCollectionFoldout(
                m_ViewModel.GetSidebarCollectionFoldoutViewModel(projectFoldout.name, parentFoldout.CollectionPath,
                    foldoutNamingState.NamingInput), m_StateManager, m_MessageManager, true);

            // A temporary foldout for a new collection doesn't have a persistent ID. Assign a unique
            // name to prevent ID conflicts with its parent in the VisualElement hierarchy.
            tempFoldout.name = $"new-collection-naming-{projectFoldout.name}";
            parentFoldout.Add(tempFoldout);

            // The original OnNamingFailed action is stale as it refers to destroyed UI elements.
            // Create a new action that correctly removes the new temporary foldout from its new parent.
            Action newOnNamingFailed = () => parentFoldout.Remove(tempFoldout);
            foldoutNamingState.OnNamingFailed = newOnNamingFailed;

            // Restore the naming state
            tempFoldout.RestoreNamingState(foldoutNamingState);
        }

        void CreateFoldoutForParentsThenItself(CollectionInfo collectionInfo, ProjectOrLibraryInfo projectOrLibraryInfo,
            SidebarFoldout projectFoldout)
        {
            var collectionId = GetCollectionId(collectionInfo);
            // Scope the search to the project foldout subtree to avoid finding foldouts from other projects
            // or stale elements that are being removed
            if (projectFoldout.Q<SidebarFoldout>(collectionId) != null)
                return;

            var collectionFoldout = new SidebarCollectionFoldout(m_ViewModel.GetSidebarCollectionFoldoutViewModel(projectOrLibraryInfo.Id, collectionInfo.GetFullPath(), collectionInfo.Name),
                m_StateManager, m_MessageManager, m_StateManager.GetCollectionFoldoutPopulatedState(collectionId));
            collectionFoldout.SetSelected(GetCollectionId(m_ViewModel.ProjectOrganizationProvider.SelectedCollection) == collectionId);

            SidebarFoldout parentFoldout = null;
            if (!string.IsNullOrEmpty(collectionInfo.ParentPath))
            {
                var parentCollection = projectOrLibraryInfo.GetCollection(collectionInfo.ParentPath);
                CreateFoldoutForParentsThenItself(parentCollection, projectOrLibraryInfo, projectFoldout);

                // Scope the search to the project foldout subtree
                parentFoldout = projectFoldout.Q<SidebarFoldout>(GetCollectionId(parentCollection));
                Utilities.DevAssert(parentFoldout != null);
            }

            var immediateParent = parentFoldout ?? projectFoldout;
            immediateParent.AddFoldout(collectionFoldout);
        }

        static void SetSelectedRecursive(SidebarFoldout foldout, string selectedId)
        {
            foldout.SetSelected(foldout.name == selectedId);
            foreach (var child in foldout.Children())
            {
                if (child is SidebarFoldout childFoldout)
                {
                    SetSelectedRecursive(childFoldout, selectedId);
                }
            }
        }

        static string GetCollectionId(CollectionInfo collectionInfo) =>
            collectionInfo == null
                ? null
                : SidebarCollectionFoldout.GetCollectionId(collectionInfo.ProjectId, collectionInfo.GetFullPath());
    }
}
