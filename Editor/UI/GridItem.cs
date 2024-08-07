using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class GridItem : VisualElement, IGridItem
    {
        static class UssStyles
        {
            public static readonly string ItemLabel = Constants.GridItemStyleClassName + "-label";
            public static readonly string ItemHighlight = Constants.GridItemStyleClassName + "-selected";
            public static readonly string ItemOverlay = Constants.GridItemStyleClassName + "-overlay";
            public static readonly string ItemIgnore = Constants.GridItemStyleClassName + "-ignore";
        }

        readonly Label m_AssetNameLabel;
        readonly AssetPreview m_AssetPreview;
        readonly LoadingIcon m_LoadingIcon;
        readonly IAssetOperationManager m_OperationManager;
        readonly IUnityConnectProxy m_UnityConnectProxy;
        readonly OperationProgressBar m_OperationProgressBar;
        readonly IPageManager m_PageManager;
        readonly IAssetDataManager m_AssetDataManager;
        readonly IUploadManager m_UploadManager;

        AssetContextMenu m_ContextMenu;
        ContextualMenuManipulator m_ContextualMenuManipulator;
        IAssetData m_AssetData;

        public IAssetData AssetData => m_AssetData;

        internal GridItem(IUnityConnectProxy unityConnectProxy, IAssetOperationManager operationManager, IPageManager pageManager, IAssetDataManager assetDataManager, IUploadManager uploadManager)
        {
            m_UnityConnectProxy = unityConnectProxy;
            m_PageManager = pageManager;
            m_OperationManager = operationManager;
            m_AssetDataManager = assetDataManager;
            m_UploadManager = uploadManager;

            AddToClassList(Constants.GridItemStyleClassName);

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            _ = new ClickOrDragStartManipulator(this, OnPointerUp, OnPointerDown, OnDragStart);

            m_AssetPreview = new AssetPreview();

            m_AssetNameLabel = new Label();
            m_AssetNameLabel.AddToClassList(UssStyles.ItemLabel);

            m_LoadingIcon = new LoadingIcon();
            UIElementsUtils.Hide(m_LoadingIcon);

            m_OperationProgressBar = new OperationProgressBar
            {
                pickingMode = PickingMode.Ignore
            };

            var overlay = new VisualElement
            {
                pickingMode = PickingMode.Ignore
            };
            overlay.AddToClassList(UssStyles.ItemOverlay);

            Add(m_AssetPreview);
            Add(m_AssetNameLabel);
            Add(m_LoadingIcon);
            Add(m_OperationProgressBar);
            Add(overlay);
        }

        void OnAssetPreviewToggleValueChanged(bool value)
        {
            if (m_AssetData == null)
                return;

            m_PageManager.ActivePage.ToggleAsset(m_AssetData, value);
        }

        public void BindWithItem(IAssetData assetData)
        {
            if (m_AssetData != null && m_AssetData.Identifier.Equals(assetData.Identifier))
                return;

            m_AssetData = assetData;

            Refresh();
        }

        void Refresh()
        {
            if (m_ContextMenu == null)
            {
                InitContextMenu(m_AssetData);
            }
            else if (!ServicesContainer.instance.Resolve<IContextMenuBuilder>()
                         .IsContextMenuMatchingAssetDataType(m_AssetData.GetType(), m_ContextMenu.GetType()))
            {
                this.RemoveManipulator(m_ContextualMenuManipulator);
                InitContextMenu(m_AssetData);
            }

            if (m_ContextMenu != null)
            {
                m_ContextMenu.TargetAssetData = m_AssetData;
            }

            RefreshHighlight();

            m_AssetNameLabel.text = m_AssetData.Name;
            m_AssetNameLabel.tooltip = m_AssetData.Name;

            m_AssetPreview.ClearPreview();

            m_OperationProgressBar.Refresh(m_OperationManager.GetAssetOperation(m_AssetData.Identifier));

            m_AssetPreview.SetStatuses(m_AssetData.PreviewStatus);

            if (m_AssetData is UploadAssetData uploadAssetData)
            {
                m_AssetPreview.EnableInClassList("asset-preview--upload", true);
                m_AssetPreview.Toggle.value = !uploadAssetData.IsIgnored;
                m_AssetPreview.Toggle.tooltip = uploadAssetData.IsIgnored ?
                    L10n.Tr(Constants.IncludeToggleTooltip) :
                    L10n.Tr(Constants.IgnoreToggleTooltip);
                m_AssetPreview.Toggle.SetEnabled(!m_UploadManager.IsUploading);

                EnableInClassList(UssStyles.ItemIgnore, uploadAssetData.IsIgnored);
                tooltip = uploadAssetData.IsIgnored ? L10n.Tr(Constants.IgnoreAssetToolTip) : "";
            }

            var tasks = new List<Task>();

            tasks.Add(m_AssetData.GetThumbnailAsync((identifier, texture2D) =>
            {
                if (!identifier.Equals(m_AssetData.Identifier))
                    return;

                m_AssetPreview.SetThumbnail(texture2D);
            }));

            tasks.Add(m_AssetData.GetPreviewStatusAsync((identifier, status) =>
            {
                if (!identifier.Equals(m_AssetData.Identifier))
                    return;

                m_AssetPreview.SetStatuses(status);
            }));

            tasks.Add(m_AssetData.ResolvePrimaryExtensionAsync((identifier, extension) =>
            {
                if (!identifier.Equals(m_AssetData.Identifier))
                    return;

                m_AssetPreview.SetAssetType(extension);
            }));

            if (m_UnityConnectProxy.AreCloudServicesReachable)
            {
                _ = WaitForResultsAsync(tasks);
            }
        }

        public event Action<PointerDownEvent> PointerDownAction;
        public event Action<PointerUpEvent> PointerUpAction;
        public event Action DragStartedAction;

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_UnityConnectProxy.OnCloudServicesReachabilityChanged += OnCloudServicesReachabilityChanged;
            m_PageManager.SelectedAssetChanged += OnSelectedAssetChanged;
            m_OperationManager.OperationProgressChanged += RefreshOperationProgress;
            m_OperationManager.OperationFinished += RefreshOperationProgress;
            m_AssetDataManager.ImportedAssetInfoChanged += OnImportedAssetInfoChanged;
            m_AssetPreview.ToggleValueChanged += OnAssetPreviewToggleValueChanged;
            m_UploadManager.UploadBegan += OnUploadBegan;
        }

        void OnCloudServicesReachabilityChanged(bool cloudServicesReachable)
        {
            Refresh();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_UnityConnectProxy.OnCloudServicesReachabilityChanged -= OnCloudServicesReachabilityChanged;
            m_PageManager.SelectedAssetChanged -= OnSelectedAssetChanged;
            m_OperationManager.OperationProgressChanged -= RefreshOperationProgress;
            m_OperationManager.OperationFinished -= RefreshOperationProgress;
            m_AssetDataManager.ImportedAssetInfoChanged -= OnImportedAssetInfoChanged;
            m_AssetPreview.ToggleValueChanged -= OnAssetPreviewToggleValueChanged;
            m_UploadManager.UploadBegan -= OnUploadBegan;
        }

        void InitContextMenu(IAssetData assetData)
        {
            m_ContextMenu = (AssetContextMenu) ServicesContainer.instance.Resolve<IContextMenuBuilder>()
                .BuildContextMenu(assetData.GetType());
            m_ContextualMenuManipulator = new ContextualMenuManipulator(m_ContextMenu.SetupContextMenuEntries);
            this.AddManipulator(m_ContextualMenuManipulator);
        }

        void RefreshOperationProgress(AssetDataOperation operation)
        {
            if (!operation.Identifier.IsSameAsset(m_AssetData?.Identifier))
                return;

            m_OperationProgressBar.Refresh(operation);
        }

        void OnImportedAssetInfoChanged(AssetChangeArgs assetChangeArgs)
        {
            if (m_AssetData == null)
                return;

            if (assetChangeArgs.Added.Any(a => a.AssetId == m_AssetData.Identifier.AssetId)
                || assetChangeArgs.Removed.Any(a => a.AssetId == m_AssetData.Identifier.AssetId)
                || assetChangeArgs.Updated.Any(a => a.AssetId == m_AssetData.Identifier.AssetId))
            {
                var assetData = m_AssetDataManager.GetAssetData(m_AssetData.Identifier);
                m_AssetData = null;
                BindWithItem(assetData);
                m_OperationProgressBar.Refresh(null);
            }
        }

        void OnSelectedAssetChanged(IPage page, IEnumerable<AssetIdentifier> assets)
        {
            RefreshHighlight();
        }

        void OnUploadBegan()
        {
            m_AssetPreview.Toggle.SetEnabled(false);
        }

        async Task WaitForResultsAsync(IReadOnlyCollection<Task> tasks)
        {
            m_LoadingIcon.PlayAnimation();
            UIElementsUtils.Show(m_LoadingIcon);

            await TaskUtils.WaitForTasksWithHandleExceptions(tasks);

            m_LoadingIcon.StopAnimation();
            UIElementsUtils.Hide(m_LoadingIcon);
        }

        void RefreshHighlight()
        {
            if (m_AssetData == null)
                return;

            var isSelected = m_PageManager.ActivePage.SelectedAssets.Any(i => i.IsSameAsset(m_AssetData.Identifier));
            if (isSelected)
            {
                AddToClassList(UssStyles.ItemHighlight);
            }
            else
            {
                RemoveFromClassList(UssStyles.ItemHighlight);
            }
        }

        void OnPointerDown(PointerDownEvent e)
        {
            PointerDownAction?.Invoke(e);
        }
        
        void OnPointerUp(PointerUpEvent e)
        {
            PointerUpAction?.Invoke(e);
        }

        void OnDragStart()
        {
            DragStartedAction?.Invoke();
        }
    }
}
