using System;
using System.Collections.Generic;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    interface IPageManager : IService
    {
        IPage ActivePage { get; }
        SortField SortField { get; }
        SortingOrder SortingOrder { get; }
        bool IsActivePage(IPage page);
        event Action<IPage> ActivePageChanged;
        event Action<IPage, bool> LoadingStatusChanged;
        event Action<IPage, IEnumerable<string>> SearchFiltersChanged;
        event Action<IPage, IEnumerable<AssetIdentifier>> SelectedAssetChanged;
        event Action<IPage, MessageData> MessageThrown;

        void SetActivePage<T>(bool forceChange = false) where T : IPage;
        void SetSortValues(SortField sortField, SortingOrder sortingOrder);
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
        IAssetOperationManager m_AssetOperationManager;

        [SerializeReference]
        IPage m_ActivePage;

        const string k_SortFieldPrefKey = "com.unity.asset-manager-for-unity.sortField";
        const string k_SortingOrderKey = "com.unity.asset-manager-for-unity.sortingOrder";

        public bool IsActivePage(IPage page) => m_ActivePage == page;

        public SortField SortField
        {
            get => (SortField)EditorPrefs.GetInt(k_SortFieldPrefKey, (int)SortField.Name);
            set => EditorPrefs.SetInt(k_SortFieldPrefKey, (int)value);
        }

        public SortingOrder SortingOrder
        {
            get => (SortingOrder)EditorPrefs.GetInt(k_SortingOrderKey, (int)SortingOrder.Ascending);
            set => EditorPrefs.SetInt(k_SortingOrderKey, (int)value);
        }

        public event Action<IPage> ActivePageChanged;
        public event Action<IPage, bool> LoadingStatusChanged;
        public event Action<IPage, IEnumerable<string>> SearchFiltersChanged;
        public event Action<IPage, IEnumerable<AssetIdentifier>> SelectedAssetChanged;
        public event Action<IPage, MessageData> MessageThrown;

        public IPage ActivePage => m_ActivePage;

        [ServiceInjection]
        public void Inject(IUnityConnectProxy unityConnectProxy, IAssetsProvider assetsProvider,
            IAssetDataManager assetDataManager, IProjectOrganizationProvider projectOrganizationProvider,
            IAssetOperationManager assetOperationManager)
        {
            m_UnityConnectProxy = unityConnectProxy;
            m_AssetsProvider = assetsProvider;
            m_AssetDataManager = assetDataManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_AssetOperationManager = assetOperationManager;
        }

        public override void OnEnable()
        {
            m_UnityConnectProxy.CloudServicesReachabilityChanged += OnCloudServicesReachabilityChanged;

            m_ActivePage?.OnEnable();
        }

        public override void OnDisable()
        {
            m_UnityConnectProxy.CloudServicesReachabilityChanged -= OnCloudServicesReachabilityChanged;

            m_ActivePage?.OnDisable();
        }

        public void SetActivePage<T>(bool forceChange = false) where T : IPage
        {
            if (!forceChange && m_ActivePage is T)
                return;

            m_ActivePage?.OnDeactivated();
            m_ActivePage?.OnDisable();

            if (typeof(T) == typeof(CollectionPage) &&
                string.IsNullOrEmpty(m_ProjectOrganizationProvider.SelectedProject?.Id))
            {
                m_ActivePage = CreatePage<AllAssetsPage>();
            }
            else
            {
                m_ActivePage = CreatePage<T>();
            }

            m_ActivePage?.OnEnable();
            m_ActivePage?.OnActivated();

            m_AssetOperationManager.ClearFinishedOperations();

            ActivePageChanged?.Invoke(m_ActivePage);
        }

        public void SetSortValues(SortField sortField, SortingOrder sortingOrder)
        {
            if(sortField == SortField && sortingOrder == SortingOrder)
                return;

            SortField = sortField;
            SortingOrder = sortingOrder;

            ActivePage?.Clear(reloadImmediately:true, clearSelection:false);
        }

        void OnCloudServicesReachabilityChanged(bool cloudServicesReachable)
        {
            if (!cloudServicesReachable && m_ActivePage is not InProjectPage)
            {
                SetActivePage<InProjectPage>();
            }
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
