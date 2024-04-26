using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IPageManager : IService
    {
        IPage ActivePage { get; }

        event Action<IPage> ActivePageChanged;
        event Action<IPage, bool> LoadingStatusChanged;
        event Action<IPage, IEnumerable<string>> SearchFiltersChanged;
        event Action<IPage, List<AssetIdentifier>> SelectedAssetChanged;
        event Action<IPage, ErrorOrMessageHandlingData> ErrorOrMessageThrown;

        void SetActivePage<T>() where T : IPage;
    }

    [Serializable]
    class PageManager : BaseService<IPageManager>, IPageManager, ISerializationCallbackReceiver
    {
        [SerializeReference]
        IAssetDataManager m_AssetDataManager;

        [SerializeReference]
        IAssetsProvider m_AssetsProvider;

        [SerializeReference]
        IProjectOrganizationProvider m_ProjectOrganizationProvider;

        [SerializeReference]
        IPage m_ActivePage;

        public event Action<IPage> ActivePageChanged = delegate { };
        public event Action<IPage, bool> LoadingStatusChanged = delegate { };
        public event Action<IPage, IEnumerable<string>> SearchFiltersChanged = delegate { };
        public event Action<IPage, List<AssetIdentifier>> SelectedAssetChanged = delegate { };
        public event Action<IPage, ErrorOrMessageHandlingData> ErrorOrMessageThrown = delegate { };

        public IPage ActivePage => m_ActivePage;

        [ServiceInjection]
        public void Inject(IAssetsProvider assetsProvider,
            IAssetDataManager assetDataManager, IProjectOrganizationProvider projectOrganizationProvider)
        {
            m_AssetsProvider = assetsProvider;
            m_AssetDataManager = assetDataManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
        }

        public override void OnEnable()
        {
            m_ProjectOrganizationProvider.ProjectSelectionChanged += ProjectSelectionChanged;

            m_ActivePage?.OnEnable();
        }

        public override void OnDisable()
        {
            m_ProjectOrganizationProvider.ProjectSelectionChanged -= ProjectSelectionChanged;

            m_ActivePage?.OnDisable();
        }

        public void SetActivePage(IPage page)
        {
            if (page == m_ActivePage)
                return;

            m_ActivePage?.OnDeactivated();

            m_ActivePage = page;
            m_ActivePage?.OnActivated();

            ActivePageChanged?.Invoke(m_ActivePage);
        }

        public void SetActivePage<T>() where T : IPage
        {
            var page = m_ActivePage is T ? m_ActivePage : CreatePage<T>();
            SetActivePage(page);
        }

        void OnUserLoginStateChange(bool isUserInfoReady, bool isUserLoggedIn)
        {
            m_ActivePage?.Clear(false);
        }

        void ProjectSelectionChanged(ProjectInfo projectInfo, CollectionInfo collectionInfo)
        {
            m_ActivePage = null; // TODO Do we need to do this

            // TODO Move this code outside this class
            // Handling page selection should happen outside the PageManager
            // Use a new class that listens to m_ProjectOrganizationProvider.ProjectSelectionChanged event and sets the active page accordingly
            SetActivePage<CollectionPage>();
        }

        void RegisterPageEvents(IPage page)
        {
            page.LoadingStatusChanged += loading => LoadingStatusChanged?.Invoke(page, loading);
            page.SelectedAssetsChanged += data => SelectedAssetChanged?.Invoke(page, data);
            page.SearchFiltersChanged += data => SearchFiltersChanged?.Invoke(page, data);
            page.ErrorOrMessageThrown += errorHandling => ErrorOrMessageThrown?.Invoke(page, errorHandling);
        }

        IPage CreatePage<T>()
        {
            var page = (IPage)Activator.CreateInstance(typeof(T), m_AssetDataManager, m_AssetsProvider, m_ProjectOrganizationProvider);
            page?.OnEnable();
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
