using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IPageManager : IService
    {
        IPage ActivePage { get; }
        bool IsActivePage(IPage page);
        event Action<IPage> ActivePageChanged;
        event Action<IPage, bool> LoadingStatusChanged;
        event Action<IPage, IEnumerable<string>> SearchFiltersChanged;
        event Action<IPage, List<AssetIdentifier>> SelectedAssetChanged;
        event Action<IPage, MessageData> MessageThrown;

        void SetActivePage<T>(bool forceChange = false) where T : IPage;
    }

    [Serializable]
    class PageManager : BaseService<IPageManager>, IPageManager, ISerializationCallbackReceiver
    {
        [SerializeReference]
        IUnityConnectProxy m_UnityConnectProxy;

        [SerializeReference]
        IAssetDataManager m_AssetDataManager;

        [SerializeReference]
        IAssetsProvider m_AssetsProvider;

        [SerializeReference]
        IProjectOrganizationProvider m_ProjectOrganizationProvider;

        [SerializeReference]
        IPage m_ActivePage;

        public bool IsActivePage(IPage page) => m_ActivePage == page;

        public event Action<IPage> ActivePageChanged;
        public event Action<IPage, bool> LoadingStatusChanged;
        public event Action<IPage, IEnumerable<string>> SearchFiltersChanged;
        public event Action<IPage, List<AssetIdentifier>> SelectedAssetChanged;
        public event Action<IPage, MessageData> MessageThrown;

        public IPage ActivePage => m_ActivePage;

        [ServiceInjection]
        public void Inject(IUnityConnectProxy unityConnectProxy, IAssetsProvider assetsProvider,
            IAssetDataManager assetDataManager, IProjectOrganizationProvider projectOrganizationProvider)
        {
            m_UnityConnectProxy = unityConnectProxy;
            m_AssetsProvider = assetsProvider;
            m_AssetDataManager = assetDataManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
        }

        public override void OnEnable()
        {
            m_UnityConnectProxy.OnCloudServicesReachabilityChanged += OnCloudServicesReachabilityChanged;

            m_ActivePage?.OnEnable();
        }

        void OnCloudServicesReachabilityChanged(bool cloudServicesReachable)
        {
            if (!cloudServicesReachable && m_ActivePage is not InProjectPage)
            {
                SetActivePage<InProjectPage>();
            }
        }

        public override void OnDisable()
        {
            m_UnityConnectProxy.OnCloudServicesReachabilityChanged -= OnCloudServicesReachabilityChanged;

            m_ActivePage?.OnDisable();
        }

        public void SetActivePage<T>(bool forceChange = false) where T : IPage
        {
            if (!forceChange && m_ActivePage is T)
                return;

            var page = CreatePage<T>();

            m_ActivePage?.OnDeactivated();
            m_ActivePage?.OnDisable();

            m_ActivePage = page;

            m_ActivePage?.OnEnable();
            m_ActivePage?.OnActivated();

            ActivePageChanged?.Invoke(m_ActivePage);
        }

        void RegisterPageEvents(IPage page)
        {
            page.LoadingStatusChanged += loading => LoadingStatusChanged?.Invoke(page, loading);
            page.SelectedAssetsChanged += data => SelectedAssetChanged?.Invoke(page, data);
            page.SearchFiltersChanged += data => SearchFiltersChanged?.Invoke(page, data);
            page.MessageThrown += errorHandling => MessageThrown?.Invoke(page, errorHandling);
        }

        IPage CreatePage<T>()
        {
            var page = (IPage)Activator.CreateInstance(typeof(T), m_AssetDataManager, m_AssetsProvider, m_ProjectOrganizationProvider, this);
            RegisterPageEvents(page);
            return page;
        }

        public void OnBeforeSerialize()
        {
            // Nothing
        }

        public void OnAfterDeserialize()
        {
            if (m_ActivePage != null)
            {
                RegisterPageEvents(m_ActivePage);
            }
        }
    }
}
