using System;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    class SidebarCollectionFoldoutViewModel
    {
        string m_CollectionPath;
        readonly string m_ProjectId;
        readonly string m_Name;
        readonly bool m_IsInAssetLibrary;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IUnityConnectProxy m_UnityConnectProxy;

        public event Action<string> ProjectInfoChanged;

        public SidebarCollectionFoldoutViewModel(string projectId, string name,
            IProjectOrganizationProvider projectOrganizationProvider, string collectionPath, bool isInAssetLibrary, IUnityConnectProxy unityConnectProxy)
        {
            m_ProjectId = projectId;
            m_CollectionPath = collectionPath;
            m_IsInAssetLibrary = isInAssetLibrary;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_UnityConnectProxy = unityConnectProxy;
            m_Name = name;
        }

        public string ProjectId => m_ProjectId;
        public string Name => m_Name;
        public string CollectionPath => m_CollectionPath;
        public bool ContextualMenuEnabled => !m_IsInAssetLibrary;

        public void BindEvents()
        {
            m_ProjectOrganizationProvider.ProjectInfoChanged += OnProjectInfoChanged;
        }

        public void UnbindEvents()
        {
            m_ProjectOrganizationProvider.ProjectInfoChanged -= OnProjectInfoChanged;
        }

        void OnProjectInfoChanged(ProjectOrLibraryInfo changedProjectInfo)
        {
            if (changedProjectInfo.Id == m_ProjectId)
            {
                ProjectInfoChanged?.Invoke(changedProjectInfo.Name);
            }
        }

        public string GetOrganizationId()
        {
            return m_ProjectOrganizationProvider?.SelectedOrganization?.Id ?? string.Empty;
        }

        public ProjectCollectionContextMenuViewModel GetProjectCollectionContextMenuViewModel()
        {
            var collectionInfo = m_CollectionPath != null
                ? CollectionInfo.CreateFromFullPath(m_ProjectOrganizationProvider.SelectedOrganization.Id, m_ProjectId,
                    m_CollectionPath)
                : null;

            return new ProjectCollectionContextMenuViewModel(m_ProjectId, m_ProjectOrganizationProvider,
                m_UnityConnectProxy, collectionInfo);
        }

        public void SelectProject()
        {
            m_ProjectOrganizationProvider.SelectProject(m_ProjectId, m_CollectionPath,
                updateProject: m_CollectionPath == null);
        }

        public async Task CreateCollection(CollectionInfo collectionInfo)
        {
            await m_ProjectOrganizationProvider.CreateCollection(collectionInfo);
        }

        public async Task RenameCollection(CollectionInfo collectionInfo, string newName)
        {
            await m_ProjectOrganizationProvider.RenameCollection(collectionInfo, newName);
        }
    }
}
