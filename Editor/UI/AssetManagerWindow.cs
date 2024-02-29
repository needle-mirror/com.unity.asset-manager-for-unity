using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class AssetManagerWindow : EditorWindow, IHasCustomMenu
    {
        static readonly Vector2 k_MinWindowSize = new(600, 250);

        public static AssetManagerWindow instance { get; private set; }
        AssetManagerWindowRoot m_Root;

        bool m_IsDocked;

        public static event Action Enabled;

        [MenuItem("Window/Asset Manager", priority = 1500)]
        static void MenuEntry()
        {
            Open();

            // Hack - We don't want to show the UploadPage when the window is opened from the menu
            var pageManager = ServicesContainer.instance.Resolve<IPageManager>();
            if (pageManager?.activePage is UploadPage)
            {
                pageManager.SetActivePage<CollectionPage>();
            }
        }

        internal static void Open()
        {
            var window = GetWindow<AssetManagerWindow>();
            window.minSize = k_MinWindowSize;
            window.titleContent = new GUIContent("Asset Manager", UIElementsUtils.GetPackageIcon());
            window.Show();
        }

        void OnEnable()
        {
            if (instance == null) instance = this;
            if (instance != this)
                return;

            m_IsDocked = docked;

            var container = ServicesContainer.instance;

            m_Root = new AssetManagerWindowRoot(
                container.Resolve<IPageManager>(),
                container.Resolve<IAssetDataManager>(),
                container.Resolve<IAssetImporter>(),
                container.Resolve<IStateManager>(),
                container.Resolve<IUnityConnectProxy>(),
                container.Resolve<IProjectOrganizationProvider>(),
                container.Resolve<ILinksProxy>(),
                container.Resolve<IEditorGUIUtilityProxy>(),
                container.Resolve<IAssetDatabaseProxy>(),
                container.Resolve<IProjectIconDownloader>());

            m_Root.RegisterCallback<GeometryChangedEvent>(OnResized);
            m_Root.OnEnable();
            m_Root.StretchToParentSize();
            rootVisualElement.Add(m_Root);

            AnalyticsSender.SendEvent(new ServicesInitializationCompletedEvent(position.size));
            Enabled?.Invoke();
        }

        void OnGUI()
        {
            m_Root.OnGUI();
        }

        void OnDisable()
        {
            if (instance == null) instance = this;
            if (instance != this)
                return;

            m_Root?.UnregisterCallback<GeometryChangedEvent>(OnResized);
            m_Root?.OnDisable();
        }

        void OnDestroy()
        {
            rootVisualElement.Remove(m_Root);
            instance = null;
        }

        void OnFocus()
        {
            if (m_Root != null && m_Root.CurrentOrganizationIsEmpty())
            {
                RefreshAll();
            }
        }

        void RefreshAll()
        {
            // Calling a manual Refresh should force a brand new initialization of the services and UI
            OnDisable();
            OnDestroy();

            ServicesContainer.instance.InitializeServices();

            OnEnable();
        }

        void Refresh()
        {
            RefreshAll();

            AnalyticsSender.SendEvent(new MenuItemSelectedEvent(MenuItemSelectedEvent.MenuItemType.Refresh));
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            var refreshItem = new GUIContent("Refresh");
            menu.AddItem(refreshItem, false, Refresh);

            m_Root?.AddItemsToMenu(menu);
        }

        void OnResized(GeometryChangedEvent evt)
        {
            if (docked == m_IsDocked)
                return;

            m_IsDocked = docked;
            AnalyticsSender.SendEvent(new WindowDockedEvent(docked));
        }
    }
}
