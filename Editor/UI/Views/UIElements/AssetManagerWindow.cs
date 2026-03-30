using System;
using System.Collections.Generic;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Upload.Editor;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class AssetManagerWindow : EditorWindow, IHasCustomMenu
    {
        static readonly Vector2 k_MinWindowSize = new(600, 250);
        static AssetManagerWindow s_Instance;

        bool m_IsDocked;
        AssetManagerWindowRoot m_Root;
        DragFromOutsideManipulator m_Manipulator;

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

            ResetSelections();
        }

        void CreateGUI()
        {
            if (s_Instance == null)
            {
                s_Instance = this;
            }

            if (s_Instance != this)
                return;

            // Guard against duplicate root creation when both RefreshAll (from registeredPackages event)
            // and Unity's own CreateGUI call run after a domain reload during package upgrades
            if (m_Root != null)
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
                container.Resolve<IPermissionsManager>(),
                container.Resolve<IUploadManager>(),
                container.Resolve<IPopupManager>(),
                container.Resolve<IAssetImportResolver>(),
                container.Resolve<IMessageManager>(),
                container.Resolve<IApplicationProxy>(),
                container.Resolve<IDialogManager>(),
                container.Resolve<ISettingsManager>(),
                container.Resolve<ISavedAssetSearchFilterManager>(),
                container.Resolve<IPackageVersionService>(),
                container.Resolve<IAssetsProvider>(),
                container.Resolve<IInlineEditService>(),
                container.Resolve<IUIPreferences>());

            m_Root.RegisterCallback<GeometryChangedEvent>(OnResized);
            m_Root.OnEnable();
            m_Root.StretchToParentSize();

            // Restore storage dismissed list before adding root so StorageInfoHelpBox sees it on first Refresh
            RestoreStorageInfoHelpBoxState(container);
            rootVisualElement.Add(m_Root);
            RestoreActionHelpBoxState(container);

            // Manipulators and Inputs
            m_Manipulator = new DragFromOutsideManipulator(rootVisualElement, container.Resolve<IPageManager>(),
                container.Resolve<IUploadManager>());
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);
            // This line is needed in order to receive the KeyDownEvent
            rootVisualElement.focusable = true;

            AnalyticsSender.SendEvent(new ServicesInitializationCompletedEvent(position.size));
            if (docked)
            {
                AnalyticsSender.SendEvent(new WindowDockedEvent(true));
            }

            // This need to be done in OnEnable to ensure the icon is the right color when switching between dark and light mode
            titleContent = new GUIContent("Asset Manager", UIElementsUtils.GetPackageIcon());

            Enabled?.Invoke();
        }

        void OnDisable()
        {
            if (s_Instance == null)
            {
                s_Instance = this;
            }

            // Always clean up this instance's resources to prevent leaks during package upgrades
            // when multiple window instances may exist
            m_Manipulator?.target.RemoveManipulator(m_Manipulator);
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnKeyDown);

            m_Root?.UnregisterCallback<GeometryChangedEvent>(OnResized);
            m_Root?.OnDisable();

            // Only disable services if this is the singleton instance
            if (s_Instance == this)
            {
                ServicesContainer.instance.OnDisable();
            }
        }

        void OnDestroy()
        {
            if (rootVisualElement.Contains(m_Root))
            {
                rootVisualElement.Remove(m_Root);
            }

            s_Instance = null;
        }

        void OnFocus()
        {
            if (m_Root != null)
            {
                m_Root.RefreshLoadingStatus();
            }
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            var refreshItem = new GUIContent("Re-initialize");
            menu.AddItem(refreshItem, false, Refresh);

            var migrationCheck = new GUIContent("Check for Tracking File Migration");
            menu.AddItem(migrationCheck, false, CheckForTrackingFileMigration);

            m_Root?.AddItemsToMenu(menu);
        }

        static void CheckForTrackingFileMigration()
        {
            var persistenceManager = ServicesContainer.instance.Resolve<IPersistenceManager>();
            persistenceManager.ReadAllEntries();
        }

        public static event Action Enabled;

        // This event is used for the Tests
        internal static event Action Refreshed;

        internal static void Open()
        {
            var window = GetWindow<AssetManagerWindow>();
            window.minSize = k_MinWindowSize;
            window.Show();
        }

        internal void RefreshAll(IService[] overrides = null)
        {
            Refreshed?.Invoke();

            // Calling a manual Refresh should force a brand new initialization of the services and UI
            OnDisable();
            OnDestroy();

            // Explicitly clear all children to prevent orphaned UI elements
            // OnDestroy only removes m_Root if Contains() succeeds, which can fail after deserialization
            rootVisualElement.Clear();
            m_Root = null;

            ServicesInitializer.ResetServices(overrides);

            CreateGUI();
        }

        void Refresh()
        {
            RefreshAll();

            AnalyticsSender.SendEvent(new MenuItemSelectedEvent(MenuItemSelectedEvent.MenuItemType.Refresh));
        }

        static void ResetSelections()
        {
            // Restore and reload the last selected organization and project
            var projectOrganizationProvider = ServicesContainer.instance.Resolve<IProjectOrganizationProvider>();
            var selectedOrganization = projectOrganizationProvider?.SelectedOrganization;
            if (selectedOrganization != null)
                projectOrganizationProvider.SelectOrganization(selectedOrganization.Id);
        }

        void OnResized(GeometryChangedEvent evt)
        {
            if (docked == m_IsDocked)
                return;

            m_IsDocked = docked;
            AnalyticsSender.SendEvent(new WindowDockedEvent(docked));
        }

        void OnKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Escape:
                    if (evt.modifiers == EventModifiers.None)
                    {
                        ServicesContainer.instance.Resolve<IPageManager>().ActivePage.LoadMore(clear: true, clearSelection: true);
                    }
                    break;
            }
        }

        static void RestoreStorageInfoHelpBoxState(ServicesContainer container)
        {
            var stateManager = container.Resolve<IStateManager>();
            var storageIds = stateManager.StorageInfoDismissedOrganizationIds;
            if (storageIds != null && storageIds.Count > 0)
                StorageInfoHelpBox.SetDismissedOrganizationIds(new List<string>(storageIds));
        }

        static void RestoreActionHelpBoxState(ServicesContainer container)
        {
            var stateManager = container.Resolve<IStateManager>();
            var messageManager = container.Resolve<IMessageManager>();
            var actionState = stateManager.GetActionHelpBoxState();
            if (!string.IsNullOrEmpty(actionState.Content))
                messageManager.SetHelpBoxMessageFromSerializedState(
                    actionState.Content,
                    actionState.MessageType,
                    actionState.Category,
                    actionState.RecommendedAction,
                    actionState.Dismissable);
        }
    }
}
