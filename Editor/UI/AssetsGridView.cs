using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class AssetsGridView : VisualElement
    {
        private GridView m_Gridview;
        private GridErrorOrMessageView m_GridErrorOrMessageView;
        private LoadingBar m_LoadingBar;

        private readonly IUnityConnectProxy m_UnityConnect;
        private readonly IPageManager m_PageManager;
        private readonly IAssetDataManager m_AssetDataManager;
        private readonly IAssetImporter m_AssetImporter;
        private readonly IThumbnailDownloader m_ThumbnailDownloader;
        private readonly IIconFactory m_IconFactory;
        private readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;

        public AssetsGridView(IProjectOrganizationProvider projectOrganizationProvider, IUnityConnectProxy unityConnect, IPageManager pageManager, IAssetDataManager assetDataManager, IAssetImporter assetImporter, IThumbnailDownloader thumbnailDownloader, IIconFactory iconFactory, ILinksProxy linksProxy)
        {
            m_UnityConnect = unityConnect;
            m_PageManager = pageManager;
            m_AssetDataManager = assetDataManager;
            m_AssetImporter = assetImporter;
            m_ThumbnailDownloader = thumbnailDownloader;
            m_IconFactory = iconFactory;
            m_ProjectOrganizationProvider = projectOrganizationProvider;

            m_Gridview = new GridView(MakeGridViewItem, BindGridViewItem);
            m_Gridview.onGridViewLastItemVisible += OnLastGridViewItemVisible;
            Add(m_Gridview);

            m_GridErrorOrMessageView = new GridErrorOrMessageView(pageManager, projectOrganizationProvider, linksProxy);
            Add(m_GridErrorOrMessageView);

            style.height = Length.Percent(100);

            m_LoadingBar = new LoadingBar();
            Add(m_LoadingBar);

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        internal void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_UnityConnect.onUserLoginStateChange += OnUserLoginStateChange;
            m_ProjectOrganizationProvider.onOrganizationInfoOrLoadingChanged += OnOrganizationInfoOrLoadingChanged;

            m_PageManager.onActivePageChanged += OnActivePageChanged;
            m_PageManager.onLoadingStatusChanged += OnLoadingStatusChanged;

            m_AssetDataManager.onImportedAssetInfoChanged += OnImportedAssetInfoChanged;
            m_PageManager.onErrorOrMessageThrown += OnErrorOrMessageThrown;

            Refresh();
        }

        internal void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_UnityConnect.onUserLoginStateChange -= OnUserLoginStateChange;
            m_ProjectOrganizationProvider.onOrganizationInfoOrLoadingChanged -= OnOrganizationInfoOrLoadingChanged;

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

        private VisualElement MakeGridViewItem() => new GridItem(m_AssetDataManager, m_AssetImporter, m_ThumbnailDownloader, m_PageManager, m_IconFactory);

        private void BindGridViewItem(VisualElement element, int index)
        {
            var item = (GridItem)element;

            var assetList = m_Gridview.ItemsSource as IList<AssetIdentifier> ?? Array.Empty<AssetIdentifier>();
            if (index < 0 || index >= assetList.Count)
                return;

            var assetId = assetList[index];
            var assetData = m_AssetDataManager.GetAssetData(assetId);
            if (assetData == null)
                return;

            item.BindWithItem(assetData);

            item.onClick += () =>
            {
                m_PageManager.activePage.selectedAssetId = assetId;
            };
        }

        private void Refresh()
        {
            UIElementsUtils.Hide(m_Gridview);
            UIElementsUtils.Hide(m_LoadingBar);
            
            var page = m_PageManager.activePage;
            // The order matters since page is null if there is a Project Level error 
            if (!m_UnityConnect.isUserLoggedIn || m_GridErrorOrMessageView.Refresh() || page == null)
                return;
            
            UIElementsUtils.Show(m_Gridview);
            m_LoadingBar.Refresh(page);
            var assetList = page.assetList.ToList();
            if (assetList.Count == 0 && page.hasMoreItems)
            {
                m_Gridview.ItemsSource = new AssetIdentifier[Constants.DefaultPageSize];
                m_Gridview.Refresh();
                m_Gridview.ResetScrollBarTop();
                if (!page.isLoading)
                    page.LoadMore();
                return;
            }

            m_Gridview.ItemsSource = assetList;
            m_Gridview.Refresh();
        }

        private void OnImportedAssetInfoChanged(AssetChangeArgs args)
        {
            //TODO: PAX-2990 Replace with individual item refresh
            Refresh();
        }

        private void OnLoadingStatusChanged(IPage page, bool _)
        {
            if (!page.isActivePage)
                return;
            Refresh();
        }

        private void OnLastGridViewItemVisible()
        {
            var page = m_PageManager.activePage;
            if (page != null && page.hasMoreItems && !page.isLoading)
            {
                page.LoadMore();
            }
            m_LoadingBar.Refresh(page, true);
        }

        private void OnErrorOrMessageThrown(IPage page, ErrorOrMessageHandlingData _)
        {
            if (!page.isActivePage)
                return;
            Refresh();
        }

        private void OnOrganizationInfoOrLoadingChanged(OrganizationInfo organization, bool isLoading)
        {
            Refresh();
        }
    }
}
