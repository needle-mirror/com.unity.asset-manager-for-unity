using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class AssetManagerWindowRoot : VisualElement
    {
        private const int k_SidebarMinWidth = 160;
        private const int k_InspectorPanelMaxWidth = 300;
        private const int k_InspectorPanelMinWidth = 200;
        private const string k_MainDarkUssName = "MainDark";
        private const string k_MainLightUssName = "MainLight";

        private VisualElement m_AssetManagerContainer;
        private VisualElement m_SearchContentSplitViewContainer;
        private VisualElement m_ContentContainer;
        private VisualElement m_AssetDetailsContainer;
        private VisualElement m_LoadingScreen;

        private LoginPage m_LoginPage;
        private SideBar m_SideBar;
        private TopBar m_TopBar;
        private Breadcrumbs m_Breadcrumbs;
        private TwoPaneSplitView m_CategoriesSplit;
        private TwoPaneSplitView m_InspectorSplit;

        private AssetsGridView m_AssetsGridView;
        private AssetDetailsPage m_AssetDetailsPage;
        private ActionHelpBox m_ActionHelpBox;

        private float m_InspectorPanelLastWidth = k_InspectorPanelMaxWidth;

        private readonly IPageManager m_PageManager;
        private readonly IAssetDataManager m_AssetDataManager;
        private readonly IAssetImporter m_AssetImporter;
        private readonly IStateManager m_StateManager;
        private readonly IUnityConnectProxy m_UnityConnect;
        private readonly IThumbnailDownloader m_ThumbnailDownloader;
        private readonly IAssetsProvider m_AssetsProvider;
        private readonly IIconFactory m_IconFactory;
        private readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        private readonly ILinksProxy m_LinksProxy;
        private readonly IEditorGUIUtilityProxy m_EditorGUIUtilityProxy;
        private readonly IAssetDatabaseProxy m_AssetDatabaseProxy;

        public AssetManagerWindowRoot(IPageManager pageManager,
            IAssetDataManager assetDataManager,
            IAssetImporter assetImporter,
            IStateManager stateManager,
            IUnityConnectProxy unityConnect,
            IAssetsProvider assetsProvider,
            IThumbnailDownloader thumbnailDownloader,
            IIconFactory iconFactory,
            IProjectOrganizationProvider projectOrganizationProvider,
            ILinksProxy linksProxy,
            IEditorGUIUtilityProxy editorGUIUtilityProxy,
            IAssetDatabaseProxy assetDatabaseProxy)
        {
            m_PageManager = pageManager;
            m_AssetDataManager = assetDataManager;
            m_AssetImporter = assetImporter;
            m_StateManager = stateManager;
            m_UnityConnect = unityConnect;
            m_AssetsProvider = assetsProvider;
            m_ThumbnailDownloader = thumbnailDownloader;
            m_IconFactory = iconFactory;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_LinksProxy = linksProxy;
            m_EditorGUIUtilityProxy = editorGUIUtilityProxy;
            m_AssetDatabaseProxy = assetDatabaseProxy;
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

        public void OnDestroy()
        {
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

            var loadingIcon = new LoadingIcon();
            loadingIcon.AddToClassList("loading-icon");

            m_LoadingScreen = new VisualElement();
            m_LoadingScreen.AddToClassList("LoadingScreen");
            var loadingScreenContainer = new VisualElement();
            loadingScreenContainer.Add(loadingIcon);
            loadingScreenContainer.Add(new Label { text = L10n.Tr("Loading...") });
            m_LoadingScreen.Add(loadingScreenContainer);
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

            m_TopBar = new TopBar(m_PageManager, m_ProjectOrganizationProvider);
            m_SearchContentSplitViewContainer.Add(m_TopBar);
            m_Breadcrumbs = new Breadcrumbs(m_PageManager, m_AssetDataManager, m_ProjectOrganizationProvider);
            m_SearchContentSplitViewContainer.Add(m_Breadcrumbs);

            var content = new VisualElement();
            content.AddToClassList("AssetManagerContentView");
            m_SearchContentSplitViewContainer.Add(content);

            m_AssetsGridView = new AssetsGridView(m_ProjectOrganizationProvider, m_UnityConnect, m_PageManager, m_AssetDataManager, m_AssetImporter, m_ThumbnailDownloader, m_IconFactory, m_LinksProxy);
            m_AssetDetailsPage = new AssetDetailsPage(m_AssetImporter, m_StateManager, m_PageManager, m_AssetDataManager, m_ThumbnailDownloader, m_IconFactory, m_EditorGUIUtilityProxy, m_AssetDatabaseProxy);

            m_AssetDetailsContainer = new VisualElement();
            m_AssetDetailsContainer.AddToClassList("AssetDetailsContainer");
            m_AssetDetailsContainer.Add(m_AssetDetailsPage);

            m_ContentContainer = new VisualElement();
            m_ContentContainer.AddToClassList("ContentPanel");
            m_ContentContainer.Add(m_CategoriesSplit);

            m_InspectorSplit.Add(m_ContentContainer);
            m_InspectorSplit.Add(m_AssetDetailsContainer);

            m_AssetManagerContainer.Add(m_InspectorSplit);

            SetInspectorVisibility(m_PageManager.activePage?.selectedAssetId);

            content.Add(m_AssetsGridView);
        }

        private void RegisterCallbacks()
        {
            m_AssetDetailsContainer.RegisterCallback<GeometryChangedEvent>(OnInspectorResized);

            m_UnityConnect.onUserLoginStateChange += OnUserLoginStateChange;
            m_PageManager.onSelectedAssetChanged += OnSelectedAssetChanged;
            m_PageManager.onActivePageChanged += OnActivePageChanged;
            m_ProjectOrganizationProvider.onOrganizationInfoOrLoadingChanged += OnOrganizationInfoOrLoadingChanged;
            m_ProjectOrganizationProvider.onProjectInfoOrLoadingChanged += OnProjectInfoOrLoadingChanged;
        }

        private void UnregisterCallbacks()
        {
            m_AssetDetailsContainer?.UnregisterCallback<GeometryChangedEvent>(OnInspectorResized);

            m_UnityConnect.onUserLoginStateChange -= OnUserLoginStateChange;
            m_PageManager.onSelectedAssetChanged -= OnSelectedAssetChanged;
            m_PageManager.onActivePageChanged -= OnActivePageChanged;
            m_ProjectOrganizationProvider.onOrganizationInfoOrLoadingChanged -= OnOrganizationInfoOrLoadingChanged;
            m_ProjectOrganizationProvider.onProjectInfoOrLoadingChanged -= OnProjectInfoOrLoadingChanged;
        }

        void OnInspectorResized(GeometryChangedEvent evt)
        {
            m_InspectorPanelLastWidth = evt.newRect.width < k_InspectorPanelMinWidth ? m_InspectorPanelLastWidth : evt.newRect.width;
        }

        private void OnUserLoginStateChange(bool isUserInfoReady, bool isUserLoggedIn) => Refresh();

        private void OnOrganizationInfoOrLoadingChanged(OrganizationInfo organizationInfo, bool isLoading) => Refresh();
        private void OnProjectInfoOrLoadingChanged(ProjectInfo projectInfo, bool isLoading) => Refresh();

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
            }
        }

        void OnActivePageChanged(IPage page)
        {
            SetInspectorVisibility(page.selectedAssetId);
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

            if (m_ProjectOrganizationProvider.isLoading)
                UIElementsUtils.Show(m_LoadingScreen);
            else
                UIElementsUtils.Hide(m_LoadingScreen);

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

        public void OnCreateGUI()
        {
        }

        public void OnFocus()
        {
            if (m_ProjectOrganizationProvider.organization?.projectInfos?.Any() != true && m_ProjectOrganizationProvider.isLoading == false)
                m_ProjectOrganizationProvider.RefreshProjects();
        }

        public void OnLostFocus()
        {
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            var refreshItem = new GUIContent("Refresh");
            if (m_ProjectOrganizationProvider is { organization: not null})
                menu.AddItem(refreshItem, false, () => m_ProjectOrganizationProvider.RefreshProjects());
            else
                menu.AddDisabledItem(refreshItem, false);

            GUIContent goToDashboard = new GUIContent(L10n.Tr("Go to Dashboard"));
            menu.AddItem(goToDashboard, false, m_LinksProxy.OpenAssetManagerDashboard);
            GUIContent projectSettings = new GUIContent(L10n.Tr("Project Settings"));
            menu.AddItem(projectSettings, false, m_LinksProxy.OpenProjectSettingsServices);
            GUIContent preferences = new GUIContent(L10n.Tr("Preferences"));
            menu.AddItem(preferences, false, m_LinksProxy.OpenPreferences);
        }
    }
}
