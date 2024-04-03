using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        }

        readonly Label m_AssetNameLabel;
        readonly AssetPreview m_AssetPreview;
        readonly OperationProgressBar m_OperationProgressBar;
        readonly LoadingIcon m_LoadingIcon;

        public IAssetData AssetData { get; private set; }

        public event Action Clicked;

        readonly IPageManager m_PageManager;
        readonly IAssetOperationManager m_OperationManager;
        
        AssetContextMenu m_ContextMenu;
        ContextualMenuManipulator m_ContextualMenuManipulator;

        internal GridItem(IAssetOperationManager operationManager, IPageManager pageManager)
        {
            m_PageManager = pageManager;
            m_OperationManager = operationManager;

            AddToClassList(Constants.GridItemStyleClassName);

            RegisterCallback<ClickEvent>(OnClick);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

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

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.onSelectedAssetChanged += OnSelectedAssetChanged;

            m_OperationManager.OperationProgressChanged += RefreshOperationProgress;
            m_OperationManager.OperationFinished += OnOperationFinalized;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.onSelectedAssetChanged -= OnSelectedAssetChanged;

            m_OperationManager.OperationProgressChanged -= RefreshOperationProgress;
            m_OperationManager.OperationFinished -= OnOperationFinalized;
        }

        void RefreshOperationProgress(AssetDataOperation operation)
        {
            if (!operation.AssetId.Equals(AssetData?.identifier))
                return;

            m_OperationProgressBar.Refresh(operation);
        }

        void OnOperationFinalized(AssetDataOperation operation)
        {
            if (!operation.AssetId.Equals(AssetData?.identifier))
                return;

            m_OperationProgressBar.Refresh(operation);

            if (operation is ImportOperation importOperation)
            {
                AssetData = null;
                BindWithItem(importOperation.assetData);
            }
        }

        private void OnSelectedAssetChanged(IPage page, AssetIdentifier assetId)
        {
            RefreshHighlight();
        }
        
        public void BindWithItem(IAssetData assetData)
        {
            if (AssetData != null && AssetData.identifier.Equals(assetData.identifier))
                return;

            AssetData = assetData;

            if (m_ContextMenu == null)
            {
                m_ContextMenu = (AssetContextMenu)ServicesContainer.instance.Resolve<IContextMenuBuilder>().BuildContextMenu(assetData.GetType());
                m_ContextualMenuManipulator = new ContextualMenuManipulator(m_ContextMenu.SetupContextMenuEntries);
                this.AddManipulator(m_ContextualMenuManipulator);
            }
            else if (!ServicesContainer.instance.Resolve<IContextMenuBuilder>().IsContextMenuMatchingAssetDataType(assetData.GetType(), m_ContextMenu.GetType()))
            {
                this.RemoveManipulator(m_ContextualMenuManipulator);
                m_ContextMenu = (AssetContextMenu)ServicesContainer.instance.Resolve<IContextMenuBuilder>().BuildContextMenu(assetData.GetType());
                m_ContextualMenuManipulator = new ContextualMenuManipulator(m_ContextMenu.SetupContextMenuEntries);
                this.AddManipulator(m_ContextualMenuManipulator);
            }
            
            if(m_ContextMenu != null)
                m_ContextMenu.TargetAssetData = assetData;
            
            RefreshHighlight();

            m_AssetNameLabel.text = assetData.name;
            m_AssetNameLabel.tooltip = assetData.name;

            m_AssetPreview.ClearPreview();

            m_OperationProgressBar.Refresh(m_OperationManager.GetAssetOperation(assetData.identifier));

            m_AssetPreview.SetStatuses(assetData.previewStatus);

            var tasks = new List<Task>();

            tasks.Add(assetData.GetThumbnailAsync((identifier, texture2D) =>
            {
                if (!identifier.Equals(AssetData.identifier))
                    return;

                m_AssetPreview.SetThumbnail(texture2D);
            }));

            tasks.Add(assetData.GetPreviewStatusAsync((identifier, status) =>
            {
                if (!identifier.Equals(AssetData.identifier))
                    return;

                m_AssetPreview.SetStatuses(status);
            }));

            tasks.Add(assetData.ResolvePrimaryExtensionAsync((identifier, extension) =>
            {
                if (!identifier.Equals(AssetData.identifier))
                    return;

                m_AssetPreview.SetAssetType(extension);
            }));

            _ = WaitForResultsAsync(tasks);
        }

        async Task WaitForResultsAsync(IEnumerable<Task> tasks)
        {
            m_LoadingIcon.PlayAnimation();
            UIElementsUtils.Show(m_LoadingIcon);

            await Utilities.WaitForTasksAndHandleExceptions(tasks);

            m_LoadingIcon.StopAnimation();
            UIElementsUtils.Hide(m_LoadingIcon);
        }

        private void RefreshHighlight()
        {
            if (AssetData == null)
                return;
            
            var isSelected = AssetData.identifier.Equals(m_PageManager.activePage?.selectedAssetId);
            if (isSelected)
            {
                AddToClassList(UssStyles.ItemHighlight);
            }
            else
            {
                RemoveFromClassList(UssStyles.ItemHighlight);
            }
        }

        void OnClick(ClickEvent _)
        {
            Clicked?.Invoke();
        }
    }
}
