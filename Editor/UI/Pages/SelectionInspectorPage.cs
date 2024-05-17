using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    abstract class SelectionInspectorPage : VisualElement
    {
        protected readonly IAssetImporter m_AssetImporter;
        protected readonly IAssetOperationManager m_AssetOperationManager;
        protected readonly IPageManager m_PageManager;
        protected readonly IAssetDataManager m_AssetDataManager;
        protected readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        protected readonly IProjectIconDownloader m_ProjectIconDownloader;
        protected readonly IPermissionsManager m_PermissionsManager;
        protected readonly IAssetDatabaseProxy m_AssetDatabaseProxy;
        protected readonly ILinksProxy m_LinksProxy;
        protected readonly IUnityConnectProxy m_UnityConnectProxy;
        protected readonly IStateManager m_StateManager;

        static string k_InspectorPageContainerUxml = "InspectorPageContainer";
        static string k_InspectorPageScrollviewClassName = "inspector-page-scrollview";
        static string k_InspectorPageCloseButtonClassName = "closeButton";
        static string k_InspectorPageTitleClassName = "inspector-page-title";

        protected ScrollView m_ScrollView;
        protected Button m_CloseButton;
        protected Label m_TitleLabel;

        protected SelectionInspectorPage(IAssetImporter assetImporter,
            IAssetOperationManager assetOperationManager,
            IStateManager stateManager,
            IPageManager pageManager,
            IAssetDataManager assetDataManager,
            IAssetDatabaseProxy assetDatabaseProxy,
            IProjectOrganizationProvider projectOrganizationProvider,
            ILinksProxy linksProxy,
            IUnityConnectProxy unityConnectProxy,
            IProjectIconDownloader projectIconDownloader,
            IPermissionsManager permissionsManager)
        {
            m_AssetImporter = assetImporter;
            m_AssetOperationManager = assetOperationManager;
            m_PageManager = pageManager;
            m_AssetDataManager = assetDataManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_ProjectIconDownloader = projectIconDownloader;
            m_PermissionsManager = permissionsManager;
            m_AssetDatabaseProxy = assetDatabaseProxy;
            m_LinksProxy = linksProxy;
            m_UnityConnectProxy = unityConnectProxy;
            m_StateManager = stateManager;
        }

        protected virtual void BuildUxmlDocument()
        {
            var treeAsset = UIElementsUtils.LoadUXML(k_InspectorPageContainerUxml);
            treeAsset.CloneTree(this);

            m_ScrollView = this.Q<ScrollView>(k_InspectorPageScrollviewClassName);
            m_ScrollView.viewDataKey = k_InspectorPageScrollviewClassName;

            m_CloseButton = this.Q<Button>(k_InspectorPageCloseButtonClassName);
            m_CloseButton.clicked += () => m_PageManager.ActivePage.ClearSelection();

            m_TitleLabel = this.Q<Label>(k_InspectorPageTitleClassName);

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        protected void RefreshScrollView()
        {
            // Bug in UI Toolkit where the scrollview does not update its size when the foldout is expanded/collapsed.
            schedule.Execute(_ => { m_ScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto; })
                .StartingIn(25);
        }

        public async Task SelectedAsset(List<AssetIdentifier> assets)
        {
            var data = new List<IAssetData>();

            foreach (var asset in assets)
            {
                var assetData = await m_AssetDataManager.GetOrSearchAssetData(asset, default);
                data.Add(assetData);
            }

            await SelectAssetDataAsync(data);
        }

        protected abstract Task SelectAssetDataAsync(List<IAssetData> assetData);

        protected virtual void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_UnityConnectProxy.OnCloudServicesReachabilityChanged += OnCloudServicesReachabilityChanged;
            m_AssetOperationManager.OperationProgressChanged += OnOperationProgress;
            m_AssetOperationManager.OperationFinished += OnOperationFinished;
            m_AssetDataManager.ImportedAssetInfoChanged += OnImportedAssetInfoChanged;
            m_AssetDataManager.AssetDataChanged += OnAssetDataChanged;
        }

        protected virtual void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_UnityConnectProxy.OnCloudServicesReachabilityChanged -= OnCloudServicesReachabilityChanged;
            m_AssetOperationManager.OperationProgressChanged -= OnOperationProgress;
            m_AssetOperationManager.OperationFinished -= OnOperationFinished;
            m_AssetDataManager.ImportedAssetInfoChanged -= OnImportedAssetInfoChanged;
            m_AssetDataManager.AssetDataChanged -= OnAssetDataChanged;
        }

        protected abstract void OnOperationProgress(AssetDataOperation operation);
        protected abstract void OnOperationFinished(AssetDataOperation operation);
        protected abstract void OnImportedAssetInfoChanged(AssetChangeArgs args);
        protected abstract void OnAssetDataChanged(AssetChangeArgs args);
        protected abstract void OnCloudServicesReachabilityChanged(bool cloudServiceReachable);
    }
}
