using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class GridItem : VisualElement
    {
        const string k_ItemLabelUssClassName = Constants.GridItemStyleClassName + "-label";
        const string k_ItemLabelHighlightUssClassName = k_ItemLabelUssClassName + "-highlight";
        const string k_OwnedAssetIconUssClassName = Constants.GridItemStyleClassName + "-owned_icon";

        public bool isMousedOver { get; set; }
        public bool isLoading { get; set; }

        GridItemHighlight m_Highlight;
        Label m_AssetNameLabel;
        AssetPreview m_AssetPreview;
        ImportProgressBar m_ImportProgressBar;

        IAssetData m_AssetData;
        LoadingIcon m_LoadingIcon;
        VisualElement m_OwnedAssetIcon;
        Material m_GeneratedMaterial;

        internal event Action onClick = delegate { };

        private readonly IAssetDataManager m_AssetDataManager;
        private readonly IAssetImporter m_AssetImporter;
        private readonly IThumbnailDownloader m_ThumbnailDownloader;
        private readonly IPageManager m_PageManager;
        private readonly IIconFactory m_IconFactory;
        internal GridItem(IAssetDataManager assetDataManager, IAssetImporter assetImporter, IThumbnailDownloader thumbnailDownloader, IPageManager pageManager, IIconFactory iconFactory)
        {
            m_AssetDataManager = assetDataManager;
            m_AssetImporter = assetImporter;
            m_ThumbnailDownloader = thumbnailDownloader;
            m_PageManager = pageManager;
            m_IconFactory = iconFactory;

            AddToClassList(Constants.GridItemStyleClassName);

            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseOutEvent>(OnMouseLeave);
            RegisterCallback<ClickEvent>(OnClick);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            m_AssetPreview = new AssetPreview(m_IconFactory);
            m_Highlight = new GridItemHighlight();
            m_AssetNameLabel = new Label();
            m_LoadingIcon = new LoadingIcon();
            m_OwnedAssetIcon = new VisualElement();
            m_ImportProgressBar = new ImportProgressBar(m_PageManager, m_AssetImporter);
            m_LoadingIcon.AddToClassList("loading-icon");

            m_AssetPreview.RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            m_AssetNameLabel.AddToClassList(k_ItemLabelUssClassName);
            m_OwnedAssetIcon.AddToClassList(k_OwnedAssetIconUssClassName);

            // UI Elements need to be ignore by the mouse so that the highlight:hover can be triggered
            m_AssetNameLabel.pickingMode = PickingMode.Ignore;
            m_OwnedAssetIcon.pickingMode = PickingMode.Ignore;
            m_ImportProgressBar.pickingMode = PickingMode.Ignore;
            m_AssetPreview.pickingMode = PickingMode.Ignore;

            Add(m_AssetPreview);
            Add(m_Highlight);
            Add(m_AssetNameLabel);
            Add(m_LoadingIcon);
            m_LoadingIcon.style.marginTop = 50;
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
            if (!operation.assetId.Equals(m_AssetData?.id))
                return;

            m_ImportProgressBar.Refresh(m_AssetImporter.GetImportOperation(m_AssetData?.id));
        }

        private void OnSelectedAssetChanged(IPage page, AssetIdentifier assetId)
        {
            RefreshHighlight();
        }

        internal void BindWithItem(IAssetData item)
        {
            // Clear the item
            Clear();

            m_AssetData = item;

            onClick = delegate { };

            // Re-add everything important
            m_AssetNameLabel.text = item.name;
            m_AssetNameLabel.pickingMode = PickingMode.Ignore;

            tooltip = item.name;
            if (m_AssetDataManager.IsInProject(item.id))
                tooltip += " (Imported)";

            Add(m_AssetPreview);
            Add(m_AssetNameLabel);

            m_ImportProgressBar.Refresh(m_AssetImporter.GetImportOperation(m_AssetData.id));

            if (m_AssetDataManager.IsInProject(item.id))
                Add(m_OwnedAssetIcon);
            
            Add(m_Highlight);
            Add(m_ImportProgressBar);
            
            m_AssetPreview.SetAssetType(item.assetType, false);
            
            m_AssetNameLabel.RemoveFromClassList(k_ItemLabelHighlightUssClassName);

            isLoading = true;
            Add(m_LoadingIcon);
            m_ThumbnailDownloader.DownloadThumbnail(item, (identifier, texture2D) =>
            {
                if (!identifier.Equals(m_AssetData.id))
                    return;
                isLoading = false;
                m_LoadingIcon.RemoveFromHierarchy();
                
                m_AssetPreview.SetThumbnail(texture2D);
            });

            RefreshHighlight();

            focusable = true;

            // First we create and add our manipulator so that the element responds context menu requests
            this.AddManipulator(new ContextualMenuManipulator(SetupContextMenuEntries));

            // We also want to be listening to the manipulator contextual menu so that we can update the action when the item is
            // already being imported
            RegisterCallback<ContextualMenuPopulateEvent>(SetupContextMenuEntries);
        }

        #region ContextMenu

        void SetupContextMenuEntries(ContextualMenuPopulateEvent evt)
        {
            ClearMenuEntries(evt);
            RemoveFromProject(evt);
            ImportEntry(evt);
            CancelImportEntry(evt);
        }

        void ClearMenuEntries(ContextualMenuPopulateEvent evt)
        {
            for (int i = 0; i < evt.menu.MenuItems().Count(); i++)
                evt.menu.MenuItems().RemoveAt(0);
        }

        void ImportEntry(ContextualMenuPopulateEvent evt)
        {
            if (m_AssetImporter.IsImporting(m_AssetData.id))
                return;

            var text = !m_AssetDataManager.IsInProject(m_AssetData.id) ? Constants.ContextMenuImport : Constants.ReImportText;
            AddMenuEntry(evt, text, m_AssetData.files.Any(), (_) =>
                m_AssetImporter.StartImportAsync(m_AssetData, ImportAction.ContextMenu));
        }

        void CancelImportEntry(ContextualMenuPopulateEvent evt)
        {
            if (m_AssetImporter.IsImporting(m_AssetData.id))
                AddMenuEntry(evt, L10n.Tr("Cancel Import"), true, (_) => m_AssetImporter.CancelImport(m_AssetData.id, true));
        }

        void AddMenuEntry(ContextualMenuPopulateEvent evt, string actionName, bool enabled, Action<DropdownMenuAction> action)
        {
            evt.menu.InsertAction(0, actionName, action, enabled ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        void RemoveFromProject(ContextualMenuPopulateEvent evt)
        {
            var enabled = m_AssetDataManager.IsInProject(m_AssetData.id) && !m_AssetImporter.IsImporting(m_AssetData.id);

            AddMenuEntry(evt, Constants.ContextMenuRemoveFromLibrary, enabled, (_) =>
            {
                m_AssetImporter.RemoveImport(m_AssetData, true);
            });
        }

        #endregion

        private void RefreshHighlight()
        {
            if (m_AssetData == null) return;

            m_Highlight = this.Q<GridItemHighlight>();
            m_Highlight.visible = isMousedOver || m_AssetData.id.Equals(m_PageManager.activePage?.selectedAssetId);
            m_AssetNameLabel = this.Q<Label>();

            if (m_Highlight.visible)
                m_AssetNameLabel.AddToClassList(k_ItemLabelHighlightUssClassName);
            else
                m_AssetNameLabel.RemoveFromClassList(k_ItemLabelHighlightUssClassName);
        }

        void OnMouseEnter(IMouseEvent x)
        {
            // There's a bug in IMouseEvent.mousePosition where it does not update while resizing window
            // The bug does not exist in Event.Current.mousePosition
            if (x.mousePosition != Event.current.mousePosition)
                return;

            isMousedOver = true;
            RefreshHighlight();
        }

        void OnMouseLeave(IMouseEvent x)
        {
            isMousedOver = false;
            RefreshHighlight();
        }

        void OnClick(ClickEvent e)
        {
            if (isLoading) return;
            onClick?.Invoke();
        }
    }
}
