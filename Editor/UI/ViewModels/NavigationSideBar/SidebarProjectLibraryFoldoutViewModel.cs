using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    class SidebarProjectLibraryFoldoutViewModel
    {
        readonly IPageManager m_PageManager;
        readonly IAssetDataManager m_AssetDataManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IUnityConnectProxy m_UnityConnectProxy;

        bool m_Enabled;

        public event Action AllInvalidated;

        public bool Enabled
        {
            get => m_Enabled;
            set
            {
                if (m_Enabled == value)
                    return;

                if (value)
                {
                    OnEnable();
                }
                else
                {
                    OnDisable();
                }

                m_Enabled = value;
            }
        }

        public event Action<ProjectOrLibraryInfo> ProjectInfoChanged;
        public event Action<List<ProjectOrLibraryInfo>> AssetLibrariesProjectsLoaded;

        public bool IsLibraryFoldout { get; }
        public IProjectOrganizationProvider ProjectOrganizationProvider => m_ProjectOrganizationProvider; //tempo


        public SidebarProjectLibraryFoldoutViewModel(bool isLibraryFoldout, IPageManager pageManager,
            IAssetDataManager assetDataManager, IProjectOrganizationProvider projectOrganizationProvider, IUnityConnectProxy unityConnectProxy)
        {
            IsLibraryFoldout = isLibraryFoldout;
            m_PageManager = pageManager;
            m_AssetDataManager = assetDataManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_UnityConnectProxy = unityConnectProxy;
        }

        public void BindEvents()
        {
            m_ProjectOrganizationProvider.ProjectInfoChanged += OnProjectInfoChanged;

            if (IsLibraryFoldout)
            {
                m_ProjectOrganizationProvider.AssetLibrariesProjectsLoaded += OnAssetLibrariesProjectsLoaded;
            }
        }

        public void UnbindEvents()
        {
            m_ProjectOrganizationProvider.ProjectInfoChanged -= OnProjectInfoChanged;

            if (IsLibraryFoldout)
            {
                m_ProjectOrganizationProvider.AssetLibrariesProjectsLoaded -= OnAssetLibrariesProjectsLoaded;
            }
        }

        void OnProjectInfoChanged(ProjectOrLibraryInfo projectOrLibraryInfo)
        {
            ProjectInfoChanged?.Invoke(projectOrLibraryInfo);
        }

        void OnAssetLibrariesProjectsLoaded(List<ProjectOrLibraryInfo> assetLibraries)
        {
            AssetLibrariesProjectsLoaded?.Invoke(assetLibraries);
        }

        void OnEnable()
        {
            m_PageManager.ActivePageChanged += OnActivePageChanged;

            m_AssetDataManager.AssetDataChanged += OnAssetDataChanged;
            m_AssetDataManager.ImportedAssetInfoChanged += OnAssetDataChanged;
        }

        void OnDisable()
        {
            m_PageManager.ActivePageChanged -= OnActivePageChanged;

            m_AssetDataManager.AssetDataChanged -= OnAssetDataChanged;
            m_AssetDataManager.ImportedAssetInfoChanged -= OnAssetDataChanged;
        }

        static bool IsLinkedToProject(BaseAssetData assetData, string projectId)
        {
            return assetData.Identifier.ProjectId == projectId || assetData.LinkedProjects != null && assetData.LinkedProjects.Any(p => p != null && p.ProjectId == projectId);
        }

        static bool ContainsLinkedCollection(BaseAssetData assetData, string projectId, string collectionPath)
        {
            return assetData.LinkedCollections != null && assetData.LinkedCollections.Any(c => c.ProjectIdentifier.ProjectId == projectId && c.CollectionPath == collectionPath);
        }

        void OnActivePageChanged(IPage page)
        {
            InvalidateAll();
        }

        void OnAssetDataChanged(AssetChangeArgs args)
        {
            InvalidateAll();
        }

        void InvalidateAll()
        {
            AllInvalidated?.Invoke();
        }

        static bool TryParseIdAsCollection(string id, out string projectId, out string collectionPath)
        {
            var parts = id.Split(new[] {"::"}, StringSplitOptions.None);
            if (parts.Length == 2)
            {
                projectId = parts[0];
                collectionPath = parts[1];
                return true;
            }

            projectId = null;
            collectionPath = null;
            return false;
        }

        public async Task<List<ProjectOrLibraryInfo>> GetAssetLibrariesAsync()
        {
            if (m_ProjectOrganizationProvider.SelectedOrganization == null || !IsLibraryFoldout)
            {
                return new List<ProjectOrLibraryInfo>();
            }

            return await m_ProjectOrganizationProvider.GetAssetLibrariesAsync();
        }

        public List<ProjectOrLibraryInfo> GetProjects()
        {
            if (m_ProjectOrganizationProvider.SelectedOrganization == null || IsLibraryFoldout)
            {
                return new List<ProjectOrLibraryInfo>();
            }

            return m_ProjectOrganizationProvider.SelectedOrganization.ProjectInfos.OrderBy(p => p.Name).ToList();
        }

        public SidebarCollectionFoldoutViewModel GetSidebarCollectionFoldoutViewModel(string projectId, string collectionPath, string name)
        {
            return new SidebarCollectionFoldoutViewModel(projectId, name, m_ProjectOrganizationProvider, collectionPath, IsLibraryFoldout, m_UnityConnectProxy);
        }
    }
}
