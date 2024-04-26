using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Net;
using Unity.Cloud.Identity;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    interface IGridItem
    {
        IAssetData AssetData { get; }
        void BindWithItem(IAssetData assetData);
    }

    class AssetsGridView : VisualElement
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
            m_Gridview.GridViewLastItemVisible += OnLastGridViewItemVisible;
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
            Services.AuthenticationStateChanged += OnAuthenticationStateChanged;
            m_ProjectOrganizationProvider.OrganizationChanged += OnOrganizationChanged;

            m_PageManager.ActivePageChanged += OnActivePageChanged;
            m_PageManager.LoadingStatusChanged += OnLoadingStatusChanged;
            m_PageManager.ErrorOrMessageThrown += OnErrorOrMessageThrown;

            Refresh();
        }

        internal void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            Services.AuthenticationStateChanged -= OnAuthenticationStateChanged;
            m_ProjectOrganizationProvider.OrganizationChanged -= OnOrganizationChanged;

            m_PageManager.ActivePageChanged -= OnActivePageChanged;
            m_PageManager.LoadingStatusChanged -= OnLoadingStatusChanged;
            m_PageManager.ErrorOrMessageThrown -= OnErrorOrMessageThrown;
        }

        void OnAuthenticationStateChanged()
        {
            Refresh();
        }

        void OnActivePageChanged(IPage page)
        {
            Refresh();
        }

        VisualElement MakeGridViewItem()
        {
            var item = new GridItem(m_AssetOperationManager, m_PageManager, m_AssetDataManager);

            item.Clicked += e =>
            {
                if(e.target is Toggle)
                    return;

                if ((e.modifiers & EventModifiers.Shift) != 0)
                {
                    var lastSelectedItemIndex = m_PageManager.ActivePage.AssetList.ToList()
                        .FindIndex(x => x.Identifier.Equals(m_PageManager.ActivePage.LastSelectedAssetId));
                    var newSelectedItemIndex = m_PageManager.ActivePage.AssetList.ToList().IndexOf(item.AssetData);

                    var selectedAssets =
                        m_PageManager.ActivePage.AssetList.ToList()
                            .GetRange(Mathf.Min(lastSelectedItemIndex, newSelectedItemIndex),
                                Mathf.Abs(newSelectedItemIndex - lastSelectedItemIndex) + 1);

                    m_PageManager.ActivePage.SelectAssets(selectedAssets.Select(x => x.Identifier).ToList());
                }
                else
                {
                    m_PageManager.ActivePage.SelectAsset(item.AssetData.Identifier,
                        (e.modifiers & (EventModifiers.Command | EventModifiers.Control)) != 0);
                }
            };

            return item;
        }

        void BindGridViewItem(VisualElement element, int index)
        {
            var assetList = m_Gridview.ItemsSource as IList<IAssetData> ?? Array.Empty<IAssetData>();
            if (index < 0 || index >= assetList.Count)
                return;

            var assetId = assetList[index];

            var item = (IGridItem)element;
            item.BindWithItem(assetId);
        }

        void Refresh()
        {
            UIElementsUtils.Hide(m_Gridview);

            var page = m_PageManager.ActivePage;

            // The order matters since page is null if there is a Project Level error
            if (!Services.AuthenticationState.Equals(AuthenticationState.LoggedIn) || m_GridErrorOrMessageView.Refresh() || page == null)
                return;

            UIElementsUtils.Show(m_Gridview);

            m_Gridview.ItemsSource = page.AssetList.ToList();
            m_Gridview.Refresh(GridView.RefreshRowsType.ResizeGridWidth);
        }

        void ClearGrid()
        {
            m_Gridview.ItemsSource = Array.Empty<IAssetData>();
            m_Gridview.Refresh(GridView.RefreshRowsType.ClearGrid);
            m_Gridview.ResetScrollBarTop();
        }

        void OnLoadingStatusChanged(IPage page, bool isLoading)
        {
            if (!page.IsActivePage)
                return;

            var hasAsset = page.AssetList?.Any() ?? false;

            if (isLoading)
            {
                m_LoadingBar.Show();
                m_LoadingBar.SetPosition(!hasAsset);
            }
            else
            {
                m_LoadingBar.Hide();
            }

            if (!page.IsLoading || !hasAsset)
            {
                Refresh();
            }
        }

        void OnLastGridViewItemVisible()
        {
            var page = m_PageManager.ActivePage;
            if (page is { CanLoadMoreItems: true, IsLoading: false })
            {
                page.LoadMore();
            }
        }

        void OnErrorOrMessageThrown(IPage page, ErrorOrMessageHandlingData _)
        {
            if (!page.IsActivePage)
                return;

            Refresh();
        }

        void OnOrganizationChanged(OrganizationInfo organization)
        {
            Refresh();
        }
    }
}
