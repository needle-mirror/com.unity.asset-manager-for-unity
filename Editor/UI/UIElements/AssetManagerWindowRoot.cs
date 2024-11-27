using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Upload.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class AssetManagerWindowRoot : VisualElement
    {
        const int k_CloudStorageUsageRefreshMs = 30000;
        const int k_SidebarMinWidth = 160;
        const int k_InspectorPanelMaxWidth = 300;
        const int k_InspectorPanelMinWidth = 200;
        const string k_MainDarkUssName = "MainDark";
        const string k_MainLightUssName = "MainLight";
        const string k_InspectorPanelLastWidthPrefKey = "InspectorPanelLastWidth";

        VisualElement m_AssetManagerContainer;
        VisualElement m_SearchContentSplitViewContainer;
        VisualElement m_ContentContainer;
        VisualElement m_SelectionInspectorContainer;
        LoadingScreen m_LoadingScreen;

        LoginPage m_LoginPage;
        AwaitingLoginPage m_AwaitingLoginPage;
        SideBar m_SideBar;
        SearchBar m_SearchBar;
        Breadcrumbs m_Breadcrumbs;
        Filters m_Filters;
        Sort m_Sort;
        TwoPaneSplitView m_CategoriesSplit;
        TwoPaneSplitView m_InspectorSplit;
        BlockingProgressPanel m_BlockingProgressPanel;

        AssetsGridView m_AssetsGridView;
        readonly List<SelectionInspectorPage> m_SelectionInspectorPages = new();
        ActionHelpBox m_ActionHelpBox;

        IVisualElementScheduledItem m_StorageInfoRefreshScheduledItem;

        VisualElement m_CustomizableSection;

        readonly IPageManager m_PageManager;
        readonly IAssetDataManager m_AssetDataManager;
        readonly IAssetImporter m_AssetImporter;
        readonly IAssetOperationManager m_AssetOperationManager;
        readonly IStateManager m_StateManager;
        readonly IUnityConnectProxy m_UnityConnect;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly ILinksProxy m_LinksProxy;
        readonly IAssetDatabaseProxy m_AssetDatabaseProxy;
        readonly IProjectIconDownloader m_ProjectIconDownloader;
        readonly IPermissionsManager m_PermissionsManager;
        readonly IUploadManager m_UploadManager;
        readonly IPopupManager m_PopupManager;
        readonly IAssetsProvider m_AssetsProvider;
        readonly IAssetImportResolver m_AssetImportResolver;
        readonly IProgressManager m_ProgressManager;

        static int InspectorPanelLastWidth
        {
            get => EditorPrefs.GetInt(k_InspectorPanelLastWidthPrefKey, k_InspectorPanelMaxWidth);
            set => EditorPrefs.SetInt(k_InspectorPanelLastWidthPrefKey, value);
        }

        public AssetManagerWindowRoot(IPageManager pageManager,
            IAssetDataManager assetDataManager,
            IAssetImporter assetImporter,
            IAssetOperationManager assetOperationManager,
            IStateManager stateManager,
            IUnityConnectProxy unityConnect,
            IProjectOrganizationProvider projectOrganizationProvider,
            ILinksProxy linksProxy,
            IAssetDatabaseProxy assetDatabaseProxy,
            IProjectIconDownloader projectIconDownloader,
            IPermissionsManager permissionsManager,
            IUploadManager uploadManager,
            IPopupManager popupManager,
            IAssetsProvider assetsProvider,
            IAssetImportResolver assetImportResolver,
            IProgressManager progressManager)
        {
            m_PageManager = pageManager;
            m_AssetDataManager = assetDataManager;
            m_AssetImporter = assetImporter;
            m_AssetOperationManager = assetOperationManager;
            m_StateManager = stateManager;
            m_UnityConnect = unityConnect;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_LinksProxy = linksProxy;
            m_AssetDatabaseProxy = assetDatabaseProxy;
            m_ProjectIconDownloader = projectIconDownloader;
            m_PermissionsManager = permissionsManager;
            m_UploadManager = uploadManager;
            m_PopupManager = popupManager;
            m_AssetsProvider = assetsProvider;
            m_AssetImportResolver = assetImportResolver;
            m_ProgressManager = progressManager;
        }

        public void OnEnable()
        {
            InitializeLayout();
            RegisterCallbacks();

            // We need to do an initial refresh here to make sure the window is correctly updated after it's opened
            // All following Refresh called will triggered through callbacks
            Refresh();
        }

        public void OnDisable()
        {
            UnregisterCallbacks();
        }

        void InitializeLayout()
        {
            UIElementsUtils.LoadCommonStyleSheet(this);
            UIElementsUtils.LoadCustomStyleSheet(this,
                EditorGUIUtility.isProSkin ? k_MainDarkUssName : k_MainLightUssName);

            m_AwaitingLoginPage  = new AwaitingLoginPage();
            m_AwaitingLoginPage.AddToClassList("SignInPage");
            m_AwaitingLoginPage.StretchToParentSize();
            Add(m_AwaitingLoginPage);

            m_LoginPage = new LoginPage();
            m_LoginPage.AddToClassList("SignInPage");
            m_LoginPage.StretchToParentSize();
            Add(m_LoginPage);

            m_AssetManagerContainer = new VisualElement();
            Add(m_AssetManagerContainer);
            m_AssetManagerContainer.AddToClassList("AssetManagerContainer");
            m_AssetManagerContainer.StretchToParentSize();

            m_PopupManager.CreatePopupContainer(this);

            m_LoadingScreen = new LoadingScreen();
            m_LoadingScreen.AddToClassList("LoadingScreen");
            m_LoadingScreen.SetVisible(false);
            m_AssetManagerContainer.Add(m_LoadingScreen);

            m_InspectorSplit =
                new TwoPaneSplitView(1, k_InspectorPanelMaxWidth, TwoPaneSplitViewOrientation.Horizontal);
            m_CategoriesSplit = new TwoPaneSplitView(0, k_SidebarMinWidth, TwoPaneSplitViewOrientation.Horizontal);

            m_SideBar = new SideBar(m_UnityConnect, m_StateManager, m_PageManager, m_ProjectOrganizationProvider,
                m_CategoriesSplit);
            m_SideBar.AddToClassList("SideBarContainer");
            m_CategoriesSplit.Add(m_SideBar);

            m_SearchContentSplitViewContainer = new VisualElement();
            m_SearchContentSplitViewContainer.AddToClassList("SearchContentSplitView");
            m_CategoriesSplit.Add(m_SearchContentSplitViewContainer);
            m_CategoriesSplit.fixedPaneInitialDimension = m_StateManager.SideBarWidth;

            var tabView = new TabView(m_PageManager, m_UnityConnect);
            tabView.AddPage<CollectionPage>(L10n.Tr(Constants.AssetsTabLabel));
            tabView.MergePage<CollectionPage, AllAssetsPage>();
            tabView.AddPage<InProjectPage>(L10n.Tr(Constants.InProjectTabLabel));
            tabView.AddPage<UploadPage>(L10n.Tr(Constants.UploadTabLabel));
            m_SearchContentSplitViewContainer.Add(tabView);

            var actionHelpBoxContainer = new VisualElement();
            actionHelpBoxContainer.AddToClassList("HelpBoxContainer");
            m_ActionHelpBox = new ActionHelpBox(m_UnityConnect, m_PageManager, m_ProjectOrganizationProvider, m_LinksProxy);
            actionHelpBoxContainer.Add(m_ActionHelpBox);
            m_SearchContentSplitViewContainer.Add(actionHelpBoxContainer);

            var storageInfoHelpBoxContainer = new VisualElement();
            storageInfoHelpBoxContainer.AddToClassList("HelpBoxContainer");
            var storageInfoHelpBox = new StorageInfoHelpBox(m_PageManager, m_ProjectOrganizationProvider, m_LinksProxy, m_AssetsProvider, m_UnityConnect);
            storageInfoHelpBoxContainer.Add(storageInfoHelpBox);
            m_SearchContentSplitViewContainer.Add(storageInfoHelpBoxContainer);

            // Schedule storage info to be refreshed each 30 seconds
            m_StorageInfoRefreshScheduledItem = storageInfoHelpBox.schedule.Execute(storageInfoHelpBox.RefreshCloudStorageAsync).Every(k_CloudStorageUsageRefreshMs);
            var topContainer = new VisualElement();
            topContainer.AddToClassList("unity-top-container");

            var topLeftContainer = new VisualElement();
            topLeftContainer.AddToClassList("unity-top-left-container");

            m_Breadcrumbs = new Breadcrumbs(m_PageManager, m_ProjectOrganizationProvider);
            topLeftContainer.Add(m_Breadcrumbs);

            var roleChip = new RoleChip(m_PageManager, m_ProjectOrganizationProvider, m_PermissionsManager);
            topLeftContainer.Add(roleChip);

            topContainer.Add(topLeftContainer);

            var topRightContainer = new VisualElement();
            topRightContainer.AddToClassList("unity-top-right-container");

            topContainer.Add(topRightContainer);

            m_SearchContentSplitViewContainer.Add(topContainer);

            m_SearchBar = new SearchBar(m_PageManager, m_ProjectOrganizationProvider);
            m_SearchContentSplitViewContainer.Add(m_SearchBar);

            var filtersSortContainer = new VisualElement();
            filtersSortContainer.AddToClassList("unity-filters-sort-container");
            m_SearchContentSplitViewContainer.Add(filtersSortContainer);

            m_Filters = new Filters(m_PageManager, m_ProjectOrganizationProvider, m_PopupManager);
            filtersSortContainer.Add(m_Filters);

            m_Sort = new Sort(m_PageManager, m_ProjectOrganizationProvider);
            filtersSortContainer.Add(m_Sort);

            var content = new VisualElement();
            content.AddToClassList("AssetManagerContentView");
            m_SearchContentSplitViewContainer.Add(content);

            if (!ServicesContainer.instance.Resolve<IContextMenuBuilder>().IsContextMenuRegistered(typeof(AssetData)))
            {
                ServicesContainer.instance.Resolve<IContextMenuBuilder>()
                    .RegisterContextMenu(typeof(AssetData), typeof(CloudAssetContextMenu));
            }

            if (!ServicesContainer.instance.Resolve<IContextMenuBuilder>()
                    .IsContextMenuRegistered(typeof(UploadAssetData)))
            {
                ServicesContainer.instance.Resolve<IContextMenuBuilder>()
                    .RegisterContextMenu(typeof(UploadAssetData), typeof(UploadContextMenu));
            }

            m_AssetsGridView = new AssetsGridView(m_ProjectOrganizationProvider, m_UnityConnect, m_PageManager,
                m_AssetDataManager, m_AssetOperationManager, m_LinksProxy, m_UploadManager, m_AssetImporter, m_AssetsProvider);

            m_SelectionInspectorPages.Add(new AssetDetailsPage(m_AssetImporter, m_AssetOperationManager, m_StateManager,
                m_PageManager, m_AssetDataManager, m_AssetDatabaseProxy, m_ProjectOrganizationProvider, m_LinksProxy,
                m_UnityConnect, m_ProjectIconDownloader, m_PermissionsManager));

            m_SelectionInspectorPages.Add(new MultiAssetDetailsPage(m_AssetImporter, m_AssetOperationManager, m_StateManager,
                m_PageManager, m_AssetDataManager, m_AssetDatabaseProxy, m_ProjectOrganizationProvider, m_LinksProxy, m_UnityConnect,
                m_ProjectIconDownloader, m_PermissionsManager));

            m_SelectionInspectorContainer = new VisualElement();
            m_SelectionInspectorContainer.AddToClassList("SelectionInspectorContainer");
            foreach (var page in m_SelectionInspectorPages)
            {
                m_SelectionInspectorContainer.Add(page);
            }

            m_ContentContainer = new VisualElement();
            m_ContentContainer.AddToClassList("ContentPanel");
            m_ContentContainer.Add(m_CategoriesSplit);

            m_InspectorSplit.Add(m_ContentContainer);
            m_InspectorSplit.Add(m_SelectionInspectorContainer);

            m_AssetManagerContainer.Add(m_InspectorSplit);

            if (m_PageManager.ActivePage?.LastSelectedAssetId == null)
            {
                SetInspectorVisibility(null);
            }
            else
            {
                SetInspectorVisibility(m_PageManager.ActivePage?.SelectedAssets);
            }

            content.Add(m_AssetsGridView);

            m_CustomizableSection = new VisualElement();
            content.Add(m_CustomizableSection);

            SetCustomFieldsVisibility(m_PageManager.ActivePage);

            // This have to be done last to be sure it's on top of everything
            m_BlockingProgressPanel = new BlockingProgressPanel();
            UIElementsUtils.Hide(m_BlockingProgressPanel);

            Add(m_BlockingProgressPanel);
        }

        void RegisterCallbacks()
        {
            m_SelectionInspectorContainer.RegisterCallback<GeometryChangedEvent>(OnInspectorResized);

            m_AssetsProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
            m_PageManager.SelectedAssetChanged += OnSelectedAssetChanged;
            m_PageManager.ActivePageChanged += OnActivePageChanged;
            m_ProjectOrganizationProvider.OrganizationChanged += OnOrganizationChanged;
            m_ProjectOrganizationProvider.LoadingStateChanged += OnLoadingStateChanged;
            m_UnityConnect.CloudServicesReachabilityChanged += OnCloudServicesReachabilityChanged;
            m_AssetImportResolver.SetConflictResolver(new AssetImportDecisionMaker());
            m_ProgressManager.Show += OnShowProgressPanel;
            m_ProgressManager.Hide += OnHideProgressPanel;
            m_ProgressManager.Progress += OnProgressPanelChanged;
        }

        void UnregisterCallbacks()
        {
            m_SelectionInspectorContainer?.UnregisterCallback<GeometryChangedEvent>(OnInspectorResized);

            m_AssetsProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
            m_PageManager.SelectedAssetChanged -= OnSelectedAssetChanged;
            m_PageManager.ActivePageChanged -= OnActivePageChanged;
            m_ProjectOrganizationProvider.OrganizationChanged -= OnOrganizationChanged;
            m_ProjectOrganizationProvider.LoadingStateChanged -= OnLoadingStateChanged;
            m_UnityConnect.CloudServicesReachabilityChanged -= OnCloudServicesReachabilityChanged;

            // Could be null if the tool is being reload after the package is updated
            if (m_ProgressManager != null)
            {
                m_ProgressManager.Show -= OnShowProgressPanel;
                m_ProgressManager.Hide -= OnHideProgressPanel;
                m_ProgressManager.Progress -= OnProgressPanelChanged;
            }
        }

        void OnInspectorResized(GeometryChangedEvent evt)
        {
            InspectorPanelLastWidth = (int) (evt.newRect.width < k_InspectorPanelMinWidth ?
                InspectorPanelLastWidth :
                evt.newRect.width);
        }

        void OnCloudServicesReachabilityChanged(bool cloudServicesReachable)
        {
            Refresh();
        }

        void OnAuthenticationStateChanged(AuthenticationState _) => Refresh();

        void OnOrganizationChanged(OrganizationInfo organizationInfo) => Refresh();

        void OnSelectedAssetChanged(IPage page, IEnumerable<AssetIdentifier> assets)
        {
            SetInspectorVisibility(assets);
        }

        void OnLoadingStateChanged(bool isLoading)
        {
            m_LoadingScreen.SetVisible(isLoading);
        }

        void SetInspectorVisibility(IEnumerable<AssetIdentifier> assets)
        {
            var validAssets = assets?.Where(asset => asset.IsIdValid()).ToList();

            if (validAssets is { Count: > 0 })
            {
                m_InspectorSplit.fixedPaneInitialDimension = InspectorPanelLastWidth;
                m_InspectorSplit.UnCollapse();

                //Hide all pages
                foreach (var inspectorPage in m_SelectionInspectorPages)
                {
                    UIElementsUtils.Hide(inspectorPage);
                }

                // Show only the first page that is visible
                var inspectorPageToShow = m_SelectionInspectorPages.Find(page => page.IsVisible(validAssets.Count));
                TaskUtils.TrackException(inspectorPageToShow?.SelectedAsset(validAssets));
                UIElementsUtils.Show(inspectorPageToShow);
            }
            else
            {
                m_InspectorSplit.fixedPaneInitialDimension = 0;
                m_InspectorSplit.CollapseChild(1);

                foreach (var inspectorPage in m_SelectionInspectorPages)
                {
                    TaskUtils.TrackException(inspectorPage?.SelectionCleared());
                }
            }
        }

        void SetCustomFieldsVisibility(IPage page)
        {
            m_CustomizableSection.Clear();

            if (page == null)
                return;

            var basePage = (BasePage)page;

            UIElementsUtils.SetDisplay(m_SearchBar, basePage.DisplaySearchBar);
            UIElementsUtils.SetDisplay(m_Filters, basePage.DisplayFilters);
            UIElementsUtils.SetDisplay(m_Sort, basePage.DisplaySort);

            if (basePage.DisplaySideBar)
            {
                m_CategoriesSplit.UnCollapse();
            }
            else
            {
                m_CategoriesSplit.CollapseChild(0);
            }

            var customSection = basePage.GetCustomUISection();

            if (customSection != null)
            {
                m_CustomizableSection.Add(customSection);
            }
        }

        void OnActivePageChanged(IPage page)
        {
            SetInspectorVisibility(page.SelectedAssets);
            SetCustomFieldsVisibility(page);
        }

        void OnProgressPanelChanged(float progress)
        {
            m_BlockingProgressPanel.SetProgress(progress);
        }

        void OnHideProgressPanel()
        {
            UIElementsUtils.Hide(m_BlockingProgressPanel);
        }

        void OnShowProgressPanel(string message)
        {
            UIElementsUtils.Show(m_BlockingProgressPanel);
            m_BlockingProgressPanel.SetProgress(0f);
            m_BlockingProgressPanel.SetMessage(message);
        }

        void Refresh()
        {
            if (!m_UnityConnect.AreCloudServicesReachable)
            {
                UIElementsUtils.Hide(m_LoginPage);
                UIElementsUtils.Hide(m_AwaitingLoginPage);
                UIElementsUtils.Show(m_AssetManagerContainer);

                m_ActionHelpBox.Refresh();
                return;
            }

            m_StorageInfoRefreshScheduledItem.Resume();

            if (m_AssetsProvider.AuthenticationState == AuthenticationState.AwaitingLogin)
            {
                UIElementsUtils.Hide(m_LoginPage);
                UIElementsUtils.Show(m_AwaitingLoginPage);
                UIElementsUtils.Hide(m_AssetManagerContainer);
                return;
            }

            if (m_AssetsProvider.AuthenticationState == AuthenticationState.LoggedOut)
            {
                UIElementsUtils.Show(m_LoginPage);
                UIElementsUtils.Hide(m_AwaitingLoginPage);
                UIElementsUtils.Hide(m_AssetManagerContainer);

                m_PermissionsManager.Reset();

                return;
            }

            if (m_AssetsProvider.AuthenticationState == AuthenticationState.LoggedIn)
            {
                UIElementsUtils.Hide(m_LoginPage);
                UIElementsUtils.Hide(m_AwaitingLoginPage);
                UIElementsUtils.Show(m_AssetManagerContainer);

                m_ActionHelpBox.Refresh();
            }
        }

        public bool CurrentOrganizationIsEmpty()
        {
            return m_ProjectOrganizationProvider.SelectedOrganization?.ProjectInfos?.Count == 0 &&
                !m_ProjectOrganizationProvider.IsLoading;
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            if (m_UnityConnect.AreCloudServicesReachable)
            {
                var goToDashboard = new GUIContent(L10n.Tr("Go to Dashboard"));
                menu.AddItem(goToDashboard, false, m_LinksProxy.OpenAssetManagerDashboard);
            }

            var projectSettings = new GUIContent(L10n.Tr("Project Settings"));
            menu.AddItem(projectSettings, false, m_LinksProxy.OpenProjectSettingsServices);

            var preferences = new GUIContent(L10n.Tr("Preferences"));
            menu.AddItem(preferences, false, m_LinksProxy.OpenPreferences);
        }
    }

    class LoadingScreen : VisualElement
    {
        readonly LoadingIcon m_LoadingIcon;

        public LoadingScreen()
        {
            m_LoadingIcon = new LoadingIcon();
            m_LoadingIcon.AddToClassList("loading-icon");

            var loadingScreenContainer = new VisualElement();
            loadingScreenContainer.Add(m_LoadingIcon);
            loadingScreenContainer.Add(new Label { text = L10n.Tr("Loading...") });

            Add(loadingScreenContainer);
        }

        public void SetVisible(bool visibility)
        {
            if (visibility)
            {
                UIElementsUtils.Show(this);
                m_LoadingIcon.PlayAnimation();
            }
            else
            {
                UIElementsUtils.Hide(this);
                m_LoadingIcon.StopAnimation();
            }
        }
    }
}
