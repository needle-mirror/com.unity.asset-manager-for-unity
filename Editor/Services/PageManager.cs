using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal interface IPageManager : IService
    {
        event Action<IPage> onActivePageChanged;
        event Action<IPage, bool> onLoadingStatusChanged;
        event Action<IPage, IReadOnlyCollection<string>> onSearchFiltersChanged;
        event Action<IPage, AssetIdentifier> onSelectedAssetChanged;
        event Action<IPage, ErrorOrMessageHandlingData> onErrorOrMessageThrown;

        IPage activePage { get; set; }
        IPage GetPage(PageType pageType, string collectionPath = null);
    }

    [Serializable]
    internal class PageManager : BaseService<IPageManager>, IPageManager, ISerializationCallbackReceiver
    {
        public event Action<IPage> onActivePageChanged = delegate { };
        public event Action<IPage, bool> onLoadingStatusChanged = delegate {};
        public event Action<IPage, IReadOnlyCollection<string>> onSearchFiltersChanged = delegate {};
        public event Action<IPage, AssetIdentifier> onSelectedAssetChanged = delegate {};
        public event Action<IPage, ErrorOrMessageHandlingData> onErrorOrMessageThrown = delegate { };

        private Dictionary<string, IPage> m_Pages = new();

        [SerializeReference]
        private IPage[] m_SerializedPages = Array.Empty<IPage>();

        public IPage activePage
        {
            get =>  m_Pages.Values.FirstOrDefault(p => p.isActivePage);
            set
            {
                var lastActivePage = activePage;
                if (value == lastActivePage)
                    return;

                value?.OnActivated();
                lastActivePage?.OnDeactivated();
                onActivePageChanged?.Invoke(activePage);
            }
        }

        private readonly IUnityConnectProxy m_UnityConnect;
        private readonly IAssetsProvider m_AssetsProvider;
        private readonly IAssetDataManager m_AssetDataManager;
        private readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        public PageManager(IUnityConnectProxy unityConnect, IAssetsProvider assetsProvider, IAssetDataManager assetDataManager, IProjectOrganizationProvider projectOrganizationProvider)
        {
            m_UnityConnect = RegisterDependency(unityConnect);
            m_AssetsProvider = RegisterDependency(assetsProvider);
            m_AssetDataManager = RegisterDependency(assetDataManager);
            m_ProjectOrganizationProvider = RegisterDependency(projectOrganizationProvider);
        }

        public override void OnEnable()
        {
            m_UnityConnect.onUserLoginStateChange += OnUserLoginStateChange;
            m_ProjectOrganizationProvider.onProjectSelectionChanged += OnProjectSelectionChanged;
            m_ProjectOrganizationProvider.onProjectInfoOrLoadingChanged += OnProjectInfoOrLoadingChanged;

            foreach (var page in m_Pages.Values)
                page.OnEnable();
        }

        public override void OnDisable()
        {
            m_UnityConnect.onUserLoginStateChange -= OnUserLoginStateChange;
            m_ProjectOrganizationProvider.onProjectSelectionChanged -= OnProjectSelectionChanged;
            m_ProjectOrganizationProvider.onProjectInfoOrLoadingChanged -= OnProjectInfoOrLoadingChanged;

            foreach (var page in m_Pages.Values)
                page.OnDisable();
        }

        private void OnUserLoginStateChange(bool isUserInfoReady, bool isUserLoggedIn)
        {
            foreach (var page in m_Pages.Values)
                page.Clear(false);
        }

        private void OnProjectSelectionChanged(ProjectInfo projectInfo)
        {
            foreach (var page in m_Pages.Values)
                page.OnDestroy();
            m_Pages.Clear();

            if (projectInfo != null)
            {
                activePage ??= GetPage(projectInfo.id == ProjectInfo.AllAssetsProjectInfo.id ? PageType.AllAssets : PageType.Collection, string.Empty);
            }
        }

        private void OnProjectInfoOrLoadingChanged(ProjectInfo projectInfo, bool isLoading)
        {
            if (!isLoading)
                return;
            
            activePage?.Clear(true);
        }

        public IPage GetPage(PageType pageType, string collectionPath = null)
        {
            var pageId = GetPageId(pageType, collectionPath);
            return m_Pages.TryGetValue(pageId, out var page) ? page : CreatePage(pageType, collectionPath);
        }

        private void RegisterPageEvents(IPage page)
        {
            page.onLoadingStatusChanged += loading => onLoadingStatusChanged?.Invoke(page, loading);
            page.onSelectedAssetChanged += data => onSelectedAssetChanged?.Invoke(page, data);
            page.onSearchFiltersChanged += data => onSearchFiltersChanged?.Invoke(page, data);
            page.onErrorOrMessageThrown += errorHandling => onErrorOrMessageThrown?.Invoke(page, errorHandling);
        }

        public void OnBeforeSerialize()
        {
            m_SerializedPages = m_Pages.Values.ToArray();
        }

        public void OnAfterDeserialize()
        {
            foreach (var page in m_SerializedPages)
            {
                ResolveDependenciesForPage(page);
                RegisterPageEvents(page);

                var pageId = GetPageId(page);
                m_Pages[pageId] = page;
            }
        }

        static string GetPageId(PageType pageType, string collectionPath = null)
        {
            return string.IsNullOrEmpty(collectionPath) ? pageType.ToString() : $"{pageType}/{collectionPath}";
        }

        static string GetPageId(IPage page)
        {
            if (page.pageType == PageType.Collection)
            {
                var collectionPage = (CollectionPage)page;
                return GetPageId(page.pageType, collectionPage.collectionPath);
            }

            return page.pageType.ToString();
        }

        IPage CreatePage(PageType pageType, string collectionPath = null)
        {
            IPage page = null;
            switch (pageType)
            {
                case PageType.Collection:
                    if (string.IsNullOrEmpty(m_ProjectOrganizationProvider.selectedProject?.id))
                        return null;

                    var collectionInfo = CollectionInfo.CreateFromFullPath(collectionPath);
                    collectionInfo.projectId = m_ProjectOrganizationProvider.selectedProject.id;
                    collectionInfo.organizationId = m_ProjectOrganizationProvider.organization.id;
                    page = new CollectionPage(m_AssetDataManager, m_AssetsProvider, m_ProjectOrganizationProvider, collectionInfo);
                    break;
                case PageType.InProject:
                    page = new InProjectPage(m_AssetDataManager);
                    break;
                case PageType.AllAssets:
                    page = new AllAssetsPage(m_AssetDataManager, m_AssetsProvider, m_ProjectOrganizationProvider);
                    break;
            }

            m_Pages[GetPageId(page)] = page;
            page?.OnEnable();
            RegisterPageEvents(page);
            return page;
        }

        private void ResolveDependenciesForPage(IPage page)
        {
            switch (page)
            {
                case CollectionPage collectionPage:
                    collectionPage.ResolveDependencies(m_AssetDataManager, m_AssetsProvider, m_ProjectOrganizationProvider);
                    break;
                case InProjectPage inProjectPage:
                    inProjectPage.ResolveDependencies(m_AssetDataManager);
                    break;
                case AllAssetsPage allAssetsPage:
                    allAssetsPage.ResolveDependencies(m_AssetDataManager, m_AssetsProvider, m_ProjectOrganizationProvider);
                    break;
            }
        }
    }
}
