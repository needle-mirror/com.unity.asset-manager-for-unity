using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class AssetsGridView : VisualElement
    {
        private GridView m_Gridview;
        ErrorView m_ErrorView;
        private LoadingBar m_LoadingBar;

        private readonly IUnityConnectProxy m_UnityConnect;
        private readonly IPageManager m_PageManager;
        private readonly IAssetDataManager m_AssetDataManager;
        private readonly IAssetImporter m_AssetImporter;
        private readonly IThumbnailDownloader m_ThumbnailDownloader;
        private readonly IIconFactory m_IconFactory;
        private readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;

        public AssetsGridView(IProjectOrganizationProvider projectOrganizationProvider, IUnityConnectProxy unityConnect, IPageManager pageManager, IAssetDataManager assetDataManager, IAssetImporter assetImporter, IThumbnailDownloader thumbnailDownloader, IIconFactory iconFactory)
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

            m_ErrorView = new ErrorView(pageManager);
            Add(m_ErrorView);

            style.height = Length.Percent(100);

            m_LoadingBar = new LoadingBar();
            Add(m_LoadingBar);

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_UnityConnect.onUserLoginStateChange += OnUserLoginStateChange;
            m_ProjectOrganizationProvider.onOrganizationInfoOrLoadingChanged += OnOrganizationInfoOrLoadingChanged;

            m_PageManager.onActivePageChanged += OnActivePageChanged;
            m_PageManager.onLoadingStatusChanged += OnLoadingStatusChanged;

            m_AssetDataManager.onImportedAssetInfoChanged += OnImportedAssetInfoChanged;
            m_PageManager.onErrorThrown += OnErrorThrown;

            Refresh();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_UnityConnect.onUserLoginStateChange -= OnUserLoginStateChange;
            m_ProjectOrganizationProvider.onOrganizationInfoOrLoadingChanged -= OnOrganizationInfoOrLoadingChanged;

            m_PageManager.onActivePageChanged -= OnActivePageChanged;
            m_PageManager.onLoadingStatusChanged -= OnLoadingStatusChanged;

            m_AssetDataManager.onImportedAssetInfoChanged -= OnImportedAssetInfoChanged;
            m_PageManager.onErrorThrown -= OnErrorThrown;
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
            if (m_ErrorView.Refresh(m_ProjectOrganizationProvider.errorHandlingData))
            {
                UIElementsUtils.Hide(m_Gridview);
                UIElementsUtils.Hide(m_LoadingBar);
                return;
            }

            var page = m_PageManager.activePage;
            if (!m_UnityConnect.isUserLoggedIn || page == null)
                return;

            if (m_ErrorView.Refresh(page.errorHandlingData))
            {
                UIElementsUtils.Hide(m_Gridview);
                UIElementsUtils.Hide(m_LoadingBar);
                return;
            }

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

        private void OnErrorThrown(IPage page, ErrorHandlingData _)
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
