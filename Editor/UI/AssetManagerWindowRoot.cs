using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class AssetManagerWindowRoot : VisualElement
    {
        const int k_SidebarMinWidth = 160;
        const int k_InspectorPanelMaxWidth = 300;
        const int k_InspectorPanelMinWidth = 200;
        const string k_MainDarkUssName = "MainDark";
        const string k_MainLightUssName = "MainLight";

        VisualElement m_AssetManagerContainer;
        VisualElement m_SearchContentSplitViewContainer;
        VisualElement m_ContentContainer;
        VisualElement m_AssetDetailsContainer;
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
        AssetDetailsPage m_AssetDetailsPage;
        ActionHelpBox m_ActionHelpBox;

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

        public AssetManagerWindowRoot(IPageManager pageManager,
            IAssetDataManager assetDataManager,
            IAssetImporter assetImporter,
            IAssetOperationManager assetOperationManager,
            IStateManager stateManager,
            IUnityConnectProxy unityConnect,
            IProjectOrganizationProvider projectOrganizationProvider,
            ILinksProxy linksProxy,
            IAssetDatabaseProxy assetDatabaseProxy,
            IProjectIconDownloader projectIconDownloader)
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

        private void InitializeLayout()
        {
            m_LoginPage = new LoginPage(m_UnityConnect);
            m_LoginPage.AddToClassList("SignInPage");
            m_LoginPage.StretchToParentSize();
            Add(m_LoginPage);

            m_AssetManagerContainer = new VisualElement();
            Add(m_AssetManagerContainer);
            m_AssetManagerContainer.AddToClassList("AssetManagerContainer");
            UIElementsUtils.LoadCommonStyleSheet(m_AssetManagerContainer);
            UIElementsUtils.LoadCustomStyleSheet(m_AssetManagerContainer, EditorGUIUtility.isProSkin ? k_MainDarkUssName : k_MainLightUssName);
            m_AssetManagerContainer.StretchToParentSize();

            m_LoadingScreen = new LoadingScreen();
            m_LoadingScreen.AddToClassList("LoadingScreen");
            m_AssetManagerContainer.Add(m_LoadingScreen);

            m_InspectorSplit = new TwoPaneSplitView(1, k_InspectorPanelMaxWidth, TwoPaneSplitViewOrientation.Horizontal);
            m_CategoriesSplit = new TwoPaneSplitView(0, k_SidebarMinWidth, TwoPaneSplitViewOrientation.Horizontal);

            m_SideBar = new SideBar(m_StateManager, m_PageManager, m_ProjectOrganizationProvider);
            m_SideBar.AddToClassList("SideBarContainer");
            m_CategoriesSplit.Add(m_SideBar);

            m_SearchContentSplitViewContainer = new VisualElement();
            m_SearchContentSplitViewContainer.AddToClassList("SearchContentSplitView");
            m_CategoriesSplit.Add(m_SearchContentSplitViewContainer);
            m_CategoriesSplit.fixedPaneInitialDimension = m_StateManager.sideBarWidth;

            var actionHelpBoxContainer = new VisualElement();
            actionHelpBoxContainer.AddToClassList("ActionHelpBoxContainer");
            m_ActionHelpBox = new ActionHelpBox(m_PageManager, m_ProjectOrganizationProvider, m_LinksProxy);
            actionHelpBoxContainer.Add(m_ActionHelpBox);
            m_SearchContentSplitViewContainer.Add(actionHelpBoxContainer);

            m_Title = new Label();
            m_Title.AddToClassList("page-title-label");
            m_SearchContentSplitViewContainer.Add(m_Title);

            m_TopBar = new TopBar(m_PageManager, m_ProjectOrganizationProvider);
            m_SearchContentSplitViewContainer.Add(m_TopBar);

            m_Breadcrumbs = new Breadcrumbs(m_PageManager, m_ProjectOrganizationProvider);
            m_SearchContentSplitViewContainer.Add(m_Breadcrumbs);

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

            if(!ServicesContainer.instance.Resolve<IContextMenuBuilder>().IsContextMenuRegistered(typeof(UploadAssetData)))
            {
                ServicesContainer.instance.Resolve<IContextMenuBuilder>()
                    .RegisterContextMenu(typeof(UploadAssetData), typeof(LocalAssetContextMenu));
            }
            
            m_AssetsGridView = new AssetsGridView(m_ProjectOrganizationProvider, m_UnityConnect, m_PageManager, m_AssetDataManager,m_AssetOperationManager, m_LinksProxy);
            m_AssetDetailsPage = new AssetDetailsPage(m_AssetImporter, m_AssetOperationManager, m_StateManager, m_PageManager, m_AssetDataManager, m_AssetDatabaseProxy, m_ProjectOrganizationProvider, m_LinksProxy, m_ProjectIconDownloader);

            m_AssetDetailsContainer = new VisualElement();
            m_AssetDetailsContainer.AddToClassList("AssetDetailsContainer");
            m_AssetDetailsContainer.Add(m_AssetDetailsPage);

            m_ContentContainer = new VisualElement();
            m_ContentContainer.AddToClassList("ContentPanel");
            m_ContentContainer.Add(m_CategoriesSplit);

            m_InspectorSplit.Add(m_ContentContainer);
            m_InspectorSplit.Add(m_AssetDetailsContainer);

            m_AssetManagerContainer.Add(m_InspectorSplit);

            m_CustomizableSection = new VisualElement();
            m_AssetManagerContainer.Add(m_CustomizableSection);

            if (m_PageManager.activePage?.selectedAssetId == null)
            {
                SetInspectorVisibility(null);
            }

            content.Add(m_AssetsGridView);

            SetCustomFieldsVisibility(m_PageManager.activePage);
        }

        private void RegisterCallbacks()
        {
            m_AssetDetailsContainer.RegisterCallback<GeometryChangedEvent>(OnInspectorResized);

            m_UnityConnect.onUserLoginStateChange += OnUserLoginStateChange;
            m_PageManager.onSelectedAssetChanged += OnSelectedAssetChanged;
            m_PageManager.onActivePageChanged += OnActivePageChanged;
            m_ProjectOrganizationProvider.OrganizationChanged += OrganizationChanged;
        }

        private void UnregisterCallbacks()
        {
            m_AssetDetailsContainer?.UnregisterCallback<GeometryChangedEvent>(OnInspectorResized);

            m_UnityConnect.onUserLoginStateChange -= OnUserLoginStateChange;
            m_PageManager.onSelectedAssetChanged -= OnSelectedAssetChanged;
            m_PageManager.onActivePageChanged -= OnActivePageChanged;
            m_ProjectOrganizationProvider.OrganizationChanged -= OrganizationChanged;
        }

        void OnInspectorResized(GeometryChangedEvent evt)
        {
            m_InspectorPanelLastWidth = evt.newRect.width < k_InspectorPanelMinWidth ? m_InspectorPanelLastWidth : evt.newRect.width;
        }

        private void OnUserLoginStateChange(bool isUserInfoReady, bool isUserLoggedIn) => Refresh();

        private void OrganizationChanged(OrganizationInfo organizationInfo) => Refresh();

        private void OnSelectedAssetChanged(IPage page, AssetIdentifier assetId)
        {
            SetInspectorVisibility(assetId);
        }

        void SetInspectorVisibility(AssetIdentifier assetId)
        {
            if (assetId == null)
            {
                m_InspectorSplit.fixedPaneInitialDimension = 0;
                m_InspectorSplit.CollapseChild(1);
            }
            else
            {
                m_InspectorSplit.fixedPaneInitialDimension = m_InspectorPanelLastWidth;
                m_InspectorSplit.UnCollapse();
                _ = m_AssetDetailsPage.SelectedAsset(assetId);
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

            if (!basePage.DisplaySideBar)
            {
                m_CategoriesSplit.fixedPaneInitialDimension = 0;
                m_CategoriesSplit.CollapseChild(0);
            }
            else
            {
                m_CategoriesSplit.UnCollapse();
                m_CategoriesSplit.fixedPaneInitialDimension = m_StateManager.sideBarWidth;
            }

            var customSection = basePage.CreateCustomUISection();

            if (customSection != null)
            {
                m_CustomizableSection.Add(customSection);
            }
        }

        void OnActivePageChanged(IPage page)
        {
            SetInspectorVisibility(page.selectedAssetId);
            SetCustomFieldsVisibility(page);
        }

        private void Refresh()
        {
            if (!m_UnityConnect.isUserLoggedIn)
            {
                UIElementsUtils.Show(m_LoginPage);
                UIElementsUtils.Hide(m_AssetManagerContainer);
                return;
            }

            UIElementsUtils.Hide(m_LoginPage);
            UIElementsUtils.Show(m_AssetManagerContainer);

            m_LoadingScreen.SetVisible(m_ProjectOrganizationProvider.isLoading);

            m_ActionHelpBox.Refresh();
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
                            m_PageManager.activePage.selectedAssetId = null;
                            e.Use();
                        }
                        break;
                    }
                case EventType.Layout:
                    {
                        var currentSceneName = SceneManager.GetActiveScene().name;
                        if (currentSceneName != m_StateManager.lastSceneName)
                        {
                            m_StateManager.lastSceneName = currentSceneName;
                            Refresh();
                        }
                        break;
                    }
            }
        }

        public bool CurrentOrganizationIsEmpty()
        {
            return m_ProjectOrganizationProvider.SelectedOrganization?.projectInfos?.Count == 0 && !m_ProjectOrganizationProvider.isLoading;
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            var goToDashboard = new GUIContent(L10n.Tr("Go to Dashboard"));
            menu.AddItem(goToDashboard, false, m_LinksProxy.OpenAssetManagerDashboard);

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
