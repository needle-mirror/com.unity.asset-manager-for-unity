using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using Unity.Cloud.Common.Runtime;

namespace Unity.AssetManager.Editor
{
    class AssetManagerWindow : EditorWindow, IHasCustomMenu
    {
        static readonly Vector2 k_MinWindowSize = new(600, 250);
        static AssetManagerWindow s_Instance;

        bool m_IsDocked;
        AssetManagerWindowRoot m_Root;

        public static AssetManagerWindow Instance => s_Instance;

        [MenuItem("Window/Asset Manager", priority = 1500)]
        static void MenuEntry()
        {
            Open();

            // Hack - We don't want to show the UploadPage when the window is opened from the menu
            var pageManager = ServicesContainer.instance.Resolve<IPageManager>();
            if (pageManager?.ActivePage is UploadPage)
            {
                pageManager.SetActivePage<CollectionPage>();
            }
        }

        void OnEnable()
        {
            if (s_Instance == null)
            {
                s_Instance = this;
            }

            if (s_Instance != this)
                return;

            m_IsDocked = docked;

            var container = ServicesContainer.instance;

            m_Root = new AssetManagerWindowRoot(
                container.Resolve<IPageManager>(),
                container.Resolve<IAssetDataManager>(),
                container.Resolve<IAssetImporter>(),
                container.Resolve<IAssetOperationManager>(),
                container.Resolve<IStateManager>(),
                container.Resolve<IUnityConnectProxy>(),
                container.Resolve<IProjectOrganizationProvider>(),
                container.Resolve<ILinksProxy>(),
                container.Resolve<IAssetDatabaseProxy>(),
                container.Resolve<IProjectIconDownloader>(),
                container.Resolve<IPermissionsManager>());

            m_Root.RegisterCallback<GeometryChangedEvent>(OnResized);
            m_Root.OnEnable();
            m_Root.StretchToParentSize();
            rootVisualElement.Add(m_Root);

            AnalyticsSender.SendEvent(new ServicesInitializationCompletedEvent(position.size));
            if (docked)
            {
                AnalyticsSender.SendEvent(new WindowDockedEvent(true));
            }

            Enabled?.Invoke();
        }

        void OnDisable()
        {
            if (s_Instance == null)
            {
                s_Instance = this;
            }

            if (s_Instance != this)
                return;

            m_Root?.UnregisterCallback<GeometryChangedEvent>(OnResized);
            m_Root?.OnDisable();
        }

        void OnDestroy()
        {
            if (rootVisualElement.Contains(m_Root))
            {
                rootVisualElement.Remove(m_Root);
            }

            s_Instance = null;
        }

        void OnGUI()
        {
            m_Root?.OnGUI();
        }

        void OnFocus()
        {
            if (m_Root != null && m_Root.CurrentOrganizationIsEmpty())
            {
                RefreshAll();
            }
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            if (ServicesContainer.instance.Resolve<IUnityConnectProxy>().AreCloudServicesReachable)
            {
                var refreshItem = new GUIContent("Refresh");
                menu.AddItem(refreshItem, false, Refresh);
            }

            m_Root?.AddItemsToMenu(menu);
        }

        public static event Action Enabled;

        internal static void Open()
        {
            var window = GetWindow<AssetManagerWindow>();
            window.minSize = k_MinWindowSize;
            window.titleContent = new GUIContent("Asset Manager", UIElementsUtils.GetPackageIcon());
            window.Show();
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

        void OnResized(GeometryChangedEvent evt)
        {
            if (docked == m_IsDocked)
                return;

            m_IsDocked = docked;
            AnalyticsSender.SendEvent(new WindowDockedEvent(docked));
        }
    }
}
