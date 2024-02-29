using System;
using UnityEngine;
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

        bool m_IsLoading;

        readonly Label m_AssetNameLabel;
        readonly AssetPreview m_AssetPreview;
        readonly OperationProgressBar m_OperationProgressBar;
        readonly LoadingIcon m_LoadingIcon;

        public IAssetData AssetData { get; private set; }

        public event Action Clicked;

        readonly IAssetDataManager m_AssetDataManager;
        readonly IAssetImporter m_AssetImporter;
        readonly IPageManager m_PageManager;

        internal GridItem(IAssetDataManager assetDataManager, IAssetImporter assetImporter, IPageManager pageManager, ILinksProxy linksProxy)
        {
            m_AssetDataManager = assetDataManager;
            m_AssetImporter = assetImporter;
            m_PageManager = pageManager;

            AddToClassList(Constants.GridItemStyleClassName);

            RegisterCallback<ClickEvent>(OnClick);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            m_AssetPreview = new AssetPreview();

            m_AssetNameLabel = new Label();
            m_AssetNameLabel.AddToClassList(UssStyles.ItemLabel);

            m_LoadingIcon = new LoadingIcon();

            m_OperationProgressBar = new OperationProgressBar(m_PageManager, m_AssetImporter)
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

            var contextMenu = new GridItemContextMenu(this, assetDataManager, m_AssetImporter, linksProxy);

            this.AddManipulator(new ContextualMenuManipulator(contextMenu.SetupContextMenuEntries));
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.onSelectedAssetChanged += OnSelectedAssetChanged;

            m_AssetImporter.onImportFinalized += OnImportProgress;
            m_AssetImporter.onImportProgress += OnImportProgress;
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.onSelectedAssetChanged -= OnSelectedAssetChanged;

            m_AssetImporter.onImportFinalized -= OnImportProgress;
            m_AssetImporter.onImportProgress -= OnImportProgress;
        }

        void OnImportProgress(ImportOperation operation)
        {
            if (!operation.assetId.Equals(AssetData?.identifier))
                return;

            m_OperationProgressBar.Refresh(operation);

            if (operation.Status != OperationStatus.InProgress)
            {
                AssetData = null;
                BindWithItem(operation.assetData);
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

            RefreshHighlight();

            // Re-add everything important
            m_AssetNameLabel.text = assetData.name;
            m_AssetNameLabel.tooltip = assetData.name;

            m_AssetPreview.ClearPreview();

            m_OperationProgressBar.Refresh(m_AssetImporter.GetImportOperation(assetData.identifier));

            m_AssetPreview.SetStatus(assetData.previewStatus);

            m_IsLoading = true;

            m_LoadingIcon.PlayAnimation();
            UIElementsUtils.Show(m_LoadingIcon);

            _ = assetData.GetThumbnailAsync((identifier, texture2D) =>
            {
                if (!identifier.Equals(AssetData.identifier))
                    return;

                m_IsLoading = false;
                m_LoadingIcon.StopAnimation();
                UIElementsUtils.Hide(m_LoadingIcon);

                m_AssetPreview.SetThumbnail(texture2D);
            });

            _ = assetData.GetPreviewStatusAsync((identifier, status) =>
            {
                if (!identifier.Equals(AssetData.identifier))
                    return;

                m_AssetPreview.SetStatus(status);
            });

            _ = assetData.ResolvePrimaryExtensionAsync((identifier, extension) =>
            {
                if (!identifier.Equals(AssetData.identifier))
                    return;

                m_AssetPreview.SetAssetType(extension, true);
            });
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

        void OnClick(ClickEvent e)
        {
            if (m_IsLoading)
                return;

            Clicked?.Invoke();
        }
    }
}
