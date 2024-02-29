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
        event Action<IPage, IEnumerable<string>> onSearchFiltersChanged;
        event Action<IPage, AssetIdentifier> onSelectedAssetChanged;
        event Action<IPage, ErrorOrMessageHandlingData> onErrorOrMessageThrown;

        IPage activePage { get; }
        void SetActivePage<T>() where T : IPage;
    }

    [Serializable]
    internal class PageManager : BaseService<IPageManager>, IPageManager, ISerializationCallbackReceiver
    {
        public event Action<IPage> onActivePageChanged = delegate { };
        public event Action<IPage, bool> onLoadingStatusChanged = delegate { };
        public event Action<IPage, IEnumerable<string>> onSearchFiltersChanged = delegate { };
        public event Action<IPage, AssetIdentifier> onSelectedAssetChanged = delegate { };
        public event Action<IPage, ErrorOrMessageHandlingData> onErrorOrMessageThrown = delegate { };

        private Dictionary<Type, IPage> m_Pages = new();

        [SerializeReference]
        private IPage[] m_SerializedPages = Array.Empty<IPage>();

        public IPage activePage => m_Pages.Values.FirstOrDefault(p => p.isActivePage);

        public void SetActivePage(IPage page)
        {
            var lastActivePage = activePage;
            if (page == lastActivePage)
                return;

            page?.OnActivated();
            lastActivePage?.OnDeactivated();
            onActivePageChanged?.Invoke(activePage);
        }

        public void SetActivePage<T>() where T : IPage
        {
            var page = m_Pages.TryGetValue(typeof(T), out var existingPage) ? existingPage : CreatePage<T>();

            SetActivePage(page);
        }

        [SerializeReference]
        IUnityConnectProxy m_UnityConnect;

        [SerializeReference]
        IAssetsProvider m_AssetsProvider;

        [SerializeReference]
        IAssetDataManager m_AssetDataManager;

        [SerializeReference]
        IProjectOrganizationProvider m_ProjectOrganizationProvider;

        [ServiceInjection]
        public void Inject(IUnityConnectProxy unityConnect, IAssetsProvider assetsProvider, IAssetDataManager assetDataManager, IProjectOrganizationProvider projectOrganizationProvider)
        {
            m_UnityConnect = unityConnect;
            m_AssetsProvider = assetsProvider;
            m_AssetDataManager = assetDataManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
        }

        public override void OnEnable()
        {
            m_UnityConnect.onUserLoginStateChange += OnUserLoginStateChange;
            m_ProjectOrganizationProvider.ProjectSelectionChanged += ProjectSelectionChanged;

            foreach (var page in m_Pages.Values)
                page.OnEnable();
        }

        public override void OnDisable()
        {
            m_UnityConnect.onUserLoginStateChange -= OnUserLoginStateChange;
            m_ProjectOrganizationProvider.ProjectSelectionChanged -= ProjectSelectionChanged;

            foreach (var page in m_Pages.Values)
                page.OnDisable();
        }

        private void OnUserLoginStateChange(bool isUserInfoReady, bool isUserLoggedIn)
        {
            foreach (var page in m_Pages.Values)
                page.Clear(false);
        }

        private void ProjectSelectionChanged(ProjectInfo projectInfo, CollectionInfo collectionInfo)
        {
            foreach (var page in m_Pages.Values)
            {
                page.OnDestroy();
            }

            m_Pages.Clear();

            if (projectInfo != null && activePage == null)
            {
                // TODO Fix Me, switching pages should not be handled by this class
                if (projectInfo.id == ProjectInfo.AllAssetsProjectInfo.id)
                {
                    SetActivePage<AllAssetsPage>();
                }
                else
                {
                    SetActivePage<CollectionPage>();
                }
            }
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
                RegisterPageEvents(page);

                m_Pages[page.GetType()] = page;
            }
        }

        IPage CreatePage<T>()
        {
            var page = (IPage)Activator.CreateInstance(typeof(T), m_AssetDataManager, m_AssetsProvider, m_ProjectOrganizationProvider);
            m_Pages[typeof(T)] = page;
            page?.OnEnable();
            RegisterPageEvents(page);
            return page;
        }
    }
}
