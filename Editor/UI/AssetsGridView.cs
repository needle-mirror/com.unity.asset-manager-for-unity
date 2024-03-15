using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    interface IGridItem
    {
        IAssetData AssetData { get; }
        void BindWithItem(IAssetData assetData);
    }

    internal class AssetsGridView : VisualElement
    {
        readonly GridView m_Gridview;
        readonly GridErrorOrMessageView m_GridErrorOrMessageView;
        readonly LoadingBar m_LoadingBar;

        readonly IUnityConnectProxy m_UnityConnect;
        readonly IPageManager m_PageManager;
        readonly IAssetDataManager m_AssetDataManager;
        readonly IAssetOperationManager m_AssetOperationManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;

        public AssetsGridView(IProjectOrganizationProvider projectOrganizationProvider,
            IUnityConnectProxy unityConnect,
            IPageManager pageManager,
            IAssetDataManager assetDataManager,
            IAssetOperationManager assetOperationManager,
            ILinksProxy linksProxy)
        {
            m_UnityConnect = unityConnect;
            m_PageManager = pageManager;
            m_AssetDataManager = assetDataManager;
            m_AssetOperationManager = assetOperationManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;

            m_Gridview = new GridView(MakeGridViewItem, BindGridViewItem);
            m_Gridview.onGridViewLastItemVisible += OnLastGridViewItemVisible;
            Add(m_Gridview);

            m_GridErrorOrMessageView = new GridErrorOrMessageView(pageManager, projectOrganizationProvider, linksProxy);
            Add(m_GridErrorOrMessageView);

            style.height = Length.Percent(100);

            m_LoadingBar = new LoadingBar();
            Add(m_LoadingBar);
            m_LoadingBar.Hide();

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        internal void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_UnityConnect.onUserLoginStateChange += OnUserLoginStateChange;
            m_ProjectOrganizationProvider.OrganizationChanged += OrganizationChanged;

            m_PageManager.onActivePageChanged += OnActivePageChanged;
            m_PageManager.onLoadingStatusChanged += OnLoadingStatusChanged;

            m_AssetDataManager.onImportedAssetInfoChanged += OnImportedAssetInfoChanged;
            m_PageManager.onErrorOrMessageThrown += OnErrorOrMessageThrown;

            Refresh();
        }

        internal void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_UnityConnect.onUserLoginStateChange -= OnUserLoginStateChange;
            m_ProjectOrganizationProvider.OrganizationChanged -= OrganizationChanged;

            m_PageManager.onActivePageChanged -= OnActivePageChanged;
            m_PageManager.onLoadingStatusChanged -= OnLoadingStatusChanged;

            m_AssetDataManager.onImportedAssetInfoChanged -= OnImportedAssetInfoChanged;
            m_PageManager.onErrorOrMessageThrown -= OnErrorOrMessageThrown;
        }

        private void OnUserLoginStateChange(bool isUserInfoReady, bool isUserLoggedIn)
        {
            Refresh();
        }

        private void OnActivePageChanged(IPage page)
        {
            Refresh();
        }

        private VisualElement MakeGridViewItem()
        {
            var item = new GridItem(m_AssetOperationManager, m_PageManager);

            item.Clicked += () =>
            {
                m_PageManager.activePage.selectedAssetId = item.AssetData.identifier;
            };

            return item;
        }

        private void BindGridViewItem(VisualElement element, int index)
        {
            var assetList = m_Gridview.ItemsSource as IList<IAssetData> ?? Array.Empty<IAssetData>();
            if (index < 0 || index >= assetList.Count)
                return;

            var assetId = assetList[index];

            var item = (IGridItem)element;
            item.BindWithItem(assetId);
        }

        private void Refresh()
        {
            UIElementsUtils.Hide(m_Gridview);

            var page = m_PageManager.activePage;

            // The order matters since page is null if there is a Project Level error
            if (!m_UnityConnect.isUserLoggedIn || m_GridErrorOrMessageView.Refresh() || page == null)
                return;

            UIElementsUtils.Show(m_Gridview);

            var assetList = page.assetList.ToList();
            if (assetList.Count == 0 && page.hasMoreItems)
            {
                ClearGrid();
                return;
            }

            m_Gridview.ItemsSource = assetList;
            m_Gridview.Refresh();
        }

        void ClearGrid()
        {
            m_Gridview.ItemsSource = Array.Empty<IAssetData>();
            m_Gridview.Refresh();
            m_Gridview.ResetScrollBarTop();
        }

        private void OnImportedAssetInfoChanged(AssetChangeArgs args)
        {
            //TODO: PAX-2990 Replace with individual item refresh
            Refresh();
        }

        private void OnLoadingStatusChanged(IPage page, bool isLoading)
        {
            if (!page.isActivePage)
                return;

            bool hasAsset = page.assetList?.Any() ?? false;

            if (isLoading)
            {
                m_LoadingBar.Show();
                m_LoadingBar.SetPosition(!hasAsset);
            }
            else
            {
                m_LoadingBar.Hide();
            }

            if (!page.isLoading || !hasAsset)
            {
                Refresh();
            }
        }

        private void OnLastGridViewItemVisible()
        {
            var page = m_PageManager.activePage;
            if (page is { hasMoreItems: true, isLoading: false })
            {
                page.LoadMore();
            }
        }

        private void OnErrorOrMessageThrown(IPage page, ErrorOrMessageHandlingData _)
        {
            if (!page.isActivePage)
                return;

            Refresh();
        }

        private void OrganizationChanged(OrganizationInfo organization)
        {
            Refresh();
        }
    }
}
