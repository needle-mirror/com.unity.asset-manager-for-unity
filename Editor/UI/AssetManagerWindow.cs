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
            m_Root = new AssetManagerWindowRoot(
                container.Resolve<IPageManager>(),
                container.Resolve<IAssetDataManager>(),
                container.Resolve<IAssetImporter>(),
                container.Resolve<IStateManager>(),
                container.Resolve<IUnityConnectProxy>(),
                container.Resolve<IAssetsProvider>(),
                container.Resolve<IThumbnailDownloader>(),
                container.Resolve<IProjectOrganizationProvider>(),
                container.Resolve<ILinksProxy>(),
                container.Resolve<IEditorGUIUtilityProxy>(),
                container.Resolve<IAssetDatabaseProxy>(),
                container.Resolve<IProjectIconDownloader>(),
                container.Resolve<IAnalyticsEngine>());
            m_Root.OnEnable();
            m_Root.StretchToParentSize();
            rootVisualElement.Add(m_Root);
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
