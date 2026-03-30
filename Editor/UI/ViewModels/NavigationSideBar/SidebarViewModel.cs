using System;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    class SidebarViewModel
    {
        readonly IUnityConnectProxy m_UnityConnectProxy;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IPageManager m_PageManager;
        readonly IAssetDataManager m_AssetDataManager;
        readonly ISavedAssetSearchFilterManager m_SavedAssetSearchFilterManager;

        public event Action CloudServicesReachabilityChanged;
        public event Action<IPage> ActivePageChanged;
        public event Action<OrganizationInfo> OrganizationChanged;
        public event Action<ProjectOrLibraryInfo, CollectionInfo> ProjectSelectionChanged;

        public bool AreCloudServicesReachable => m_UnityConnectProxy.AreCloudServicesReachable;

        public SidebarViewModel(IUnityConnectProxy unityConnectProxy, IProjectOrganizationProvider projectOrganizationProvider, IPageManager pageManager,
            IAssetDataManager assetDataManager, ISavedAssetSearchFilterManager savedAssetSearchFilterManager)
        {
            m_UnityConnectProxy = unityConnectProxy;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_PageManager = pageManager;
            m_AssetDataManager = assetDataManager;
            m_SavedAssetSearchFilterManager = savedAssetSearchFilterManager;
        }

        public void BindEvents()
        {
            m_UnityConnectProxy.CloudServicesReachabilityChanged += OnCloudServicesReachabilityChanged;
            m_PageManager.ActivePageChanged += OnActivePageChanged;
            m_ProjectOrganizationProvider.OrganizationChanged += OnOrganizationChanged;
            m_ProjectOrganizationProvider.ProjectSelectionChanged += OnProjectSelectionChanged;
        }

        public void UnbindEvents()
        {
            m_UnityConnectProxy.CloudServicesReachabilityChanged -= OnCloudServicesReachabilityChanged;
            m_PageManager.ActivePageChanged -= OnActivePageChanged;
            m_ProjectOrganizationProvider.OrganizationChanged -= OnOrganizationChanged;
            m_ProjectOrganizationProvider.ProjectSelectionChanged -= OnProjectSelectionChanged;
        }

        private void OnCloudServicesReachabilityChanged(bool obj)
        {
            CloudServicesReachabilityChanged?.Invoke();
            ActivePageChanged?.Invoke(m_PageManager.ActivePage);
        }

        private void OnActivePageChanged(IPage page)
        {
            ActivePageChanged?.Invoke(page);
        }

        private void OnOrganizationChanged(OrganizationInfo organizationInfo)
        {
            OrganizationChanged?.Invoke(organizationInfo);
        }

        private void OnProjectSelectionChanged(ProjectOrLibraryInfo projectOrLibrary, CollectionInfo collection)
        {
            ProjectSelectionChanged?.Invoke(projectOrLibrary, collection);
        }

        public SidebarSavedViewsFoldoutViewModel CreateSavedViewsFoldoutViewModel()
        {
            return new SidebarSavedViewsFoldoutViewModel(m_ProjectOrganizationProvider, m_PageManager, m_SavedAssetSearchFilterManager);
        }

        public SidebarProjectLibraryFoldoutViewModel CreateProjectLibraryFoldoutViewModel(bool isLibraryFoldout)
        {
            return new SidebarProjectLibraryFoldoutViewModel(isLibraryFoldout, m_PageManager, m_AssetDataManager, m_ProjectOrganizationProvider, m_UnityConnectProxy);
        }

        public bool IsCurrentlySelectingAnAssetLibrary()
        {
            return m_ProjectOrganizationProvider.SelectedAssetLibrary != null;
        }

        public OrganizationInfo GetSelectedOrganization()
        {
            return m_ProjectOrganizationProvider.SelectedOrganization;
        }

        public ProjectOrLibraryInfo GetSelectedProjectOrLibrary()
        {
            return m_ProjectOrganizationProvider.SelectedProjectOrLibrary;
        }

        public CollectionInfo GetSelectedCollection()
        {
            return m_ProjectOrganizationProvider.SelectedCollection;
        }

        /// <summary>
        /// Resolves project and collection from persisted ids for restoring selection after domain reload.
        /// Returns (null, null) if the organization is not loaded or the project/collection are not found.
        /// </summary>
        public (ProjectOrLibraryInfo project, CollectionInfo collection) TryGetProjectAndCollectionFromIds(string organizationId, string projectId, string collectionPath)
        {
            var org = m_ProjectOrganizationProvider.SelectedOrganization;
            if (org == null || org.Id != organizationId)
                return (null, null);

            var project = m_ProjectOrganizationProvider.GetProject(projectId);
            if (project == null)
                return (null, null);

            var collection = string.IsNullOrEmpty(collectionPath) ? null : project.GetCollection(collectionPath);
            return (project, collection ?? new CollectionInfo(organizationId, projectId, null));
        }

        public IPage GetActivePage()
        {
            return m_PageManager.ActivePage;
        }

        public void SelectAllAssetPage()
        {
            m_PageManager.SetActivePage<AllAssetsPage>();
        }
    }
}
