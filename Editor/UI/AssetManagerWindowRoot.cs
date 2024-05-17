using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Cloud.Identity;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class AssetManagerWindowRoot : VisualElement
    {
        const int k_CloudStorageUsageRefreshMs = 30000;
        const int k_SidebarMinWidth = 160;
        const int k_InspectorPanelMaxWidth = 300;
        const int k_InspectorPanelMinWidth = 200;
        const string k_MainDarkUssName = "MainDark";
        const string k_MainLightUssName = "MainLight";

        VisualElement m_AssetManagerContainer;
        VisualElement m_SearchContentSplitViewContainer;
        VisualElement m_ContentContainer;
        VisualElement m_SelectionInspectorContainer;
        LoadingScreen m_LoadingScreen;

        LoginPage m_LoginPage;
        SideBar m_SideBar;
        Label m_Title;
        TopBar m_TopBar;
        Breadcrumbs m_Breadcrumbs;
        Filters m_Filters;
        TwoPaneSplitView m_CategoriesSplit;
        TwoPaneSplitView m_InspectorSplit;

        AssetsGridView m_AssetsGridView;
        List<SelectionInspectorPage> m_SelectionInspectorPages = new();
        ActionHelpBox m_ActionHelpBox;

        IVisualElementScheduledItem m_StorageInfoRefreshScheduledItem;

        VisualElement m_CustomizableSection;

        float m_InspectorPanelLastWidth = k_InspectorPanelMaxWidth;

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
            IPermissionsManager permissionsManager)
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
        }

        public void OnEnable()
        {
            InitializeLayout();
            RegisterCallbacks();

            Services.InitAuthenticatedServices();

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
            m_LoginPage = new LoginPage();
            m_LoginPage.AddToClassList("SignInPage");
            m_LoginPage.StretchToParentSize();
            Add(m_LoginPage);

            m_AssetManagerContainer = new VisualElement();
            Add(m_AssetManagerContainer);
            m_AssetManagerContainer.AddToClassList("AssetManagerContainer");
            UIElementsUtils.LoadCommonStyleSheet(m_AssetManagerContainer);
            UIElementsUtils.LoadCustomStyleSheet(m_AssetManagerContainer,
                EditorGUIUtility.isProSkin ? k_MainDarkUssName : k_MainLightUssName);
            m_AssetManagerContainer.StretchToParentSize();

            m_LoadingScreen = new LoadingScreen();
            m_LoadingScreen.AddToClassList("LoadingScreen");
            m_AssetManagerContainer.Add(m_LoadingScreen);

            m_InspectorSplit =
                new TwoPaneSplitView(1, k_InspectorPanelMaxWidth, TwoPaneSplitViewOrientation.Horizontal);
            m_CategoriesSplit = new TwoPaneSplitView(0, k_SidebarMinWidth, TwoPaneSplitViewOrientation.Horizontal);

            m_SideBar = new SideBar(m_UnityConnect, m_StateManager, m_PageManager, m_ProjectOrganizationProvider, m_CategoriesSplit);
            m_SideBar.AddToClassList("SideBarContainer");
            m_CategoriesSplit.Add(m_SideBar);

            m_SearchContentSplitViewContainer = new VisualElement();
            m_SearchContentSplitViewContainer.AddToClassList("SearchContentSplitView");
            m_CategoriesSplit.Add(m_SearchContentSplitViewContainer);
            m_CategoriesSplit.fixedPaneInitialDimension = m_StateManager.SideBarWidth;

            var actionHelpBoxContainer = new VisualElement();
            actionHelpBoxContainer.AddToClassList("ActionHelpBoxContainer");
            m_ActionHelpBox = new ActionHelpBox(m_UnityConnect, m_PageManager, m_ProjectOrganizationProvider, m_LinksProxy);
            actionHelpBoxContainer.Add(m_ActionHelpBox);
            m_SearchContentSplitViewContainer.Add(actionHelpBoxContainer);

            var storageInfoHelpBoxContainer = new VisualElement();
            storageInfoHelpBoxContainer.AddToClassList("StorageInfoHelpBoxContainer");
            var unityConnectProxy = ServicesContainer.instance.Resolve<IUnityConnectProxy>();
            var assetProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
            var storageInfoHelpBox = new StorageInfoHelpBox(m_ProjectOrganizationProvider, m_LinksProxy, assetProvider, unityConnectProxy);
            storageInfoHelpBoxContainer.Add(storageInfoHelpBox);
            m_SearchContentSplitViewContainer.Add(storageInfoHelpBoxContainer);

            // Schedule storage info to be refreshed each 30 seconds
            m_StorageInfoRefreshScheduledItem = storageInfoHelpBox.schedule.Execute(storageInfoHelpBox.RefreshCloudStorageAsync).Every(k_CloudStorageUsageRefreshMs);

            m_Title = new Label();
            m_Title.AddToClassList("page-title-label");
            m_SearchContentSplitViewContainer.Add(m_Title);

            m_TopBar = new TopBar(m_PageManager, m_ProjectOrganizationProvider);
            m_SearchContentSplitViewContainer.Add(m_TopBar);

            var breadcrumbsAndRoleContainer = new VisualElement();
            breadcrumbsAndRoleContainer.AddToClassList("unity-breadcrumbs-and-role-container");

            m_Breadcrumbs = new Breadcrumbs(m_PageManager, m_ProjectOrganizationProvider);
            breadcrumbsAndRoleContainer.Add(m_Breadcrumbs);

            var roleChip = new RoleChip(m_PageManager, m_ProjectOrganizationProvider, m_PermissionsManager);
            breadcrumbsAndRoleContainer.Add(roleChip);

            m_SearchContentSplitViewContainer.Add(breadcrumbsAndRoleContainer);

            m_Filters = new Filters(m_PageManager, m_ProjectOrganizationProvider);
            m_SearchContentSplitViewContainer.Add(m_Filters);

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
                m_AssetDataManager, m_AssetOperationManager, m_LinksProxy);

            m_SelectionInspectorPages.Add(new AssetDetailsPage(m_AssetImporter, m_AssetOperationManager, m_StateManager,
                m_PageManager, m_AssetDataManager, m_AssetDatabaseProxy, m_ProjectOrganizationProvider, m_LinksProxy, m_UnityConnect,
                m_ProjectIconDownloader, m_PermissionsManager));

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

            m_CustomizableSection = new VisualElement();
            m_AssetManagerContainer.Add(m_CustomizableSection);

            if (m_PageManager.ActivePage?.LastSelectedAssetId == null)
            {
                SetInspectorVisibility(null);
            }

            content.Add(m_AssetsGridView);

            SetCustomFieldsVisibility(m_PageManager.ActivePage);
        }

        void RegisterCallbacks()
        {
            m_SelectionInspectorContainer.RegisterCallback<GeometryChangedEvent>(OnInspectorResized);

            Services.AuthenticationStateChanged += OnAuthenticationStateChanged;
            m_PageManager.SelectedAssetChanged += OnSelectedAssetChanged;
            m_PageManager.ActivePageChanged += OnActivePageChanged;
            m_ProjectOrganizationProvider.OrganizationChanged += OrganizationChanged;
            m_UnityConnect.OnCloudServicesReachabilityChanged += OnCloudServicesReachabilityChanged;
        }

        void UnregisterCallbacks()
        {
            m_SelectionInspectorContainer?.UnregisterCallback<GeometryChangedEvent>(OnInspectorResized);

            Services.AuthenticationStateChanged -= OnAuthenticationStateChanged;
            m_PageManager.SelectedAssetChanged -= OnSelectedAssetChanged;
            m_PageManager.ActivePageChanged -= OnActivePageChanged;
            m_ProjectOrganizationProvider.OrganizationChanged -= OrganizationChanged;
            m_UnityConnect.OnCloudServicesReachabilityChanged -= OnCloudServicesReachabilityChanged;
        }

        void OnInspectorResized(GeometryChangedEvent evt)
        {
            m_InspectorPanelLastWidth = evt.newRect.width < k_InspectorPanelMinWidth ?
                m_InspectorPanelLastWidth :
                evt.newRect.width;
        }

        void OnCloudServicesReachabilityChanged(bool cloudServicesReachable)
        {
            Refresh();
        }

        void OnAuthenticationStateChanged() => Refresh();

        void OrganizationChanged(OrganizationInfo organizationInfo) => Refresh();

        void OnSelectedAssetChanged(IPage page, List<AssetIdentifier> assets)
        {
            SetInspectorVisibility(assets);
        }

        void SetInspectorVisibility(List<AssetIdentifier> assets)
        {
            List<AssetIdentifier> validAssets = assets?.Where(asset => asset.IsIdValid()).ToList();

            if (validAssets is { Count: > 0 })
            {
                m_InspectorSplit.fixedPaneInitialDimension = m_InspectorPanelLastWidth;
                m_InspectorSplit.UnCollapse();

                foreach (var page in m_SelectionInspectorPages)
                {
                    _ = page.SelectedAsset(assets);
                }
            }
            else
            {
                m_InspectorSplit.fixedPaneInitialDimension = 0;
                m_InspectorSplit.CollapseChild(1);
            }
        }

        void SetCustomFieldsVisibility(IPage page)
        {
            m_CustomizableSection.Clear();

            if (page == null)
                return;

            var basePage = (BasePage)page;

            if (!string.IsNullOrEmpty(basePage.Title))
            {
                m_Title.text = basePage.Title;
                UIElementsUtils.Show(m_Title);
            }
            else
            {
                UIElementsUtils.Hide(m_Title);
            }

            UIElementsUtils.SetDisplay(m_TopBar, basePage.DisplayTopBar);

            if (basePage.DisplaySideBar)
            {
                m_CategoriesSplit.UnCollapse();
            }
            else
            {
                m_CategoriesSplit.CollapseChild(0);
            }

            var customSection = basePage.CreateCustomUISection();

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

        void Refresh()
        {
            if (!m_UnityConnect.AreCloudServicesReachable)
            {
                UIElementsUtils.Hide(m_LoginPage);
                UIElementsUtils.Show(m_AssetManagerContainer);

                m_LoadingScreen.SetVisible(false);
                m_ActionHelpBox.Refresh();
                return;
            }

            m_StorageInfoRefreshScheduledItem.Resume();

            if (Services.AuthenticationState == AuthenticationState.AwaitingLogin)
            {
                UIElementsUtils.Hide(m_LoginPage);
                UIElementsUtils.Hide(m_AssetManagerContainer);
                return;
            }

            if (Services.AuthenticationState == AuthenticationState.LoggedOut)
            {
                UIElementsUtils.Show(m_LoginPage);
                UIElementsUtils.Hide(m_AssetManagerContainer);
                return;
            }

            if (Services.AuthenticationState == AuthenticationState.LoggedIn)
            {
                UIElementsUtils.Hide(m_LoginPage);
                UIElementsUtils.Show(m_AssetManagerContainer);

                m_LoadingScreen.SetVisible(m_ProjectOrganizationProvider.IsLoading);
                m_ActionHelpBox.Refresh();
            }
        }

        public void OnGUI()
        {
            var e = Event.current;
            switch (e.type)
            {
                case EventType.KeyDown:
                {
                    if (e.keyCode == KeyCode.Escape && e.modifiers == EventModifiers.None)
                    {
                        m_PageManager.ActivePage.Clear(true);
                        e.Use();
                    }

                    break;
                }
                case EventType.Layout:
                {
                    var currentSceneName = SceneManager.GetActiveScene().name;
                    if (currentSceneName != m_StateManager.LastSceneName)
                    {
                        m_StateManager.LastSceneName = currentSceneName;
                        Refresh();
                    }

                    break;
                }
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
