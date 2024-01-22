using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class AssetManagerWindow : EditorWindow, IHasCustomMenu
    {
        private static readonly Vector2 k_MinWindowSize = new Vector2(600, 250);

        public static AssetManagerWindow instance { get; private set; }
        private AssetManagerWindowRoot m_Root;

        [MenuItem("Window/Asset Manager", priority = 1500)]
        internal static void Open()
        {
            var window = GetWindow<AssetManagerWindow>();
            window.minSize = k_MinWindowSize;
            window.Show();
        }

        private void OnEnable()
        {
            if (instance == null) instance = this;
            if (instance != this)
                return;
            titleContent = new GUIContent("Asset Manager", UIElementsUtils.GetPackageIcon());

            var container = ServicesContainer.instance;
            // The services container only enables a service (and its dependencies) when it is resolved so that we can keep amount of services
            // running in the background to as low as possible if the user never opens the Window. However, this also means not all services are enabled
            // when the Window is opened, so we need to manually do that here.
            container.EnableAllServices();
            m_Root = new AssetManagerWindowRoot(
                container.Resolve<IPageManager>(),
                container.Resolve<IAssetDataManager>(),
                container.Resolve<IAssetImporter>(),
                container.Resolve<IStateManager>(),
                container.Resolve<IUnityConnectProxy>(),
                container.Resolve<IAssetsProvider>(),
                container.Resolve<IThumbnailDownloader>(),
                container.Resolve<IIconFactory>(),
                container.Resolve<IProjectOrganizationProvider>(),
                container.Resolve<ILinksProxy>(),
                container.Resolve<IEditorGUIUtilityProxy>(),
                container.Resolve<IAssetDatabaseProxy>());
            m_Root.OnEnable();
            m_Root.StretchToParentSize();
            rootVisualElement.Add(m_Root);
        }

        private void CreateGUI()
        {
            m_Root.OnCreateGUI();
        }

        private void OnGUI()
        {
            m_Root.OnGUI();
        }

        private void OnDisable()
        {
            if (instance == null) instance = this;
            if (instance != this)
                return;

            m_Root?.OnDisable();
        }

        private void OnDestroy()
        {
            m_Root?.OnDestroy();
            instance = null;
        }

        private void OnFocus()
        {
            m_Root?.OnFocus();
        }

        private void OnLostFocus()
        {
            m_Root?.OnLostFocus();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            m_Root?.AddItemsToMenu(menu);
        }
    }
}
