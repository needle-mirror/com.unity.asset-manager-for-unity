using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Random = System.Random;

namespace Unity.AssetManager.Editor
{
    internal class GridItem : VisualElement
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
        readonly ImportProgressBar m_ImportProgressBar;
        readonly LoadingIcon m_LoadingIcon;
        
        IAssetData m_AssetData;

        internal event Action onClick = delegate { };

        private readonly IAssetDataManager m_AssetDataManager;
        private readonly IAssetImporter m_AssetImporter;
        private readonly IThumbnailDownloader m_ThumbnailDownloader;
        private readonly IPageManager m_PageManager;
        private readonly ILinksProxy m_LinksProxy;

        internal GridItem(IAssetDataManager assetDataManager, IAssetImporter assetImporter, IThumbnailDownloader thumbnailDownloader, IPageManager pageManager, ILinksProxy linksProxy)
        {
            m_AssetDataManager = assetDataManager;
            m_AssetImporter = assetImporter;
            m_ThumbnailDownloader = thumbnailDownloader;
            m_PageManager = pageManager;
            m_LinksProxy = linksProxy;

            AddToClassList(Constants.GridItemStyleClassName);
            
            RegisterCallback<ClickEvent>(OnClick);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            m_AssetPreview = new AssetPreview();
            
            m_AssetNameLabel = new Label();
            m_AssetNameLabel.AddToClassList(UssStyles.ItemLabel);
            
            m_LoadingIcon = new LoadingIcon();
            m_LoadingIcon.AddToClassList("loading-icon");
            
            m_ImportProgressBar = new ImportProgressBar(m_PageManager, m_AssetImporter)
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
            Add(m_ImportProgressBar);
            Add(overlay);
           
            // First we create and add our manipulator so that the element responds context menu requests
            this.AddManipulator(new ContextualMenuManipulator(SetupContextMenuEntries));

            // We also want to be listening to the manipulator contextual menu so that we can update the action when the item is
            // already being imported
            RegisterCallback<ContextualMenuPopulateEvent>(SetupContextMenuEntries);
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
            if (!operation.assetId.Equals(m_AssetData?.identifier))
                return;

            m_ImportProgressBar.Refresh(m_AssetImporter.GetImportOperation(m_AssetData?.identifier));
        }

        private void OnSelectedAssetChanged(IPage page, AssetIdentifier assetId)
        {
            RefreshHighlight();
        }

        internal async void BindWithItem(IAssetData item)
        {
            if (m_AssetData != null && m_AssetData.identifier.Equals(item.identifier))
                return;

            m_AssetData = item;

            onClick = delegate { };
            
            RefreshHighlight();

            // Re-add everything important
            m_AssetNameLabel.text = item.name;
            m_AssetNameLabel.tooltip = item.name;

            m_AssetPreview.ClearPreview();

            m_ImportProgressBar.Refresh(m_AssetImporter.GetImportOperation(item.identifier));

            m_AssetPreview.SetImportStatusIcon(ImportedStatus.None);

            m_IsLoading = true;
            
            m_LoadingIcon.PlayAnimation();
            UIElementsUtils.Show(m_LoadingIcon);

            m_ThumbnailDownloader.DownloadThumbnail(item, (identifier, texture2D) =>
            {
                if (!identifier.Equals(m_AssetData.identifier))
                    return;
                
                m_IsLoading = false;
                m_LoadingIcon.StopAnimation();
                UIElementsUtils.Hide(m_LoadingIcon);

                m_AssetPreview.SetThumbnail(texture2D);
            });

            _ = m_AssetDataManager.GetImportedStatus(item.identifier, (identifier, status) =>
            {
                if (!identifier.Equals(m_AssetData.identifier))
                    return;
                
                m_AssetPreview.SetImportStatusIcon(status);
            });
            
            _ = item.GetPrimaryExtension((identifier, extension) =>
            {
                if (!identifier.Equals(m_AssetData.identifier))
                    return;
                
                m_AssetPreview.SetAssetType(extension, true);
            });
        }

        void SetupContextMenuEntries(ContextualMenuPopulateEvent evt)
        {
            ClearMenuEntries(evt);
            RemoveFromProject(evt);
            ShowInProject(evt);
            ShowInDashboard(evt);
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
            if (m_AssetImporter.IsImporting(m_AssetData.identifier))
                return;

            var text = !m_AssetDataManager.IsInProject(m_AssetData.identifier) ? Constants.ContextMenuImport : Constants.ReimportText;

            AddMenuEntry(evt, text, true, (_) =>
                m_AssetImporter.StartImportAsync(m_AssetData, ImportAction.ContextMenu));
        }

        void CancelImportEntry(ContextualMenuPopulateEvent evt)
        {
            if (m_AssetImporter.IsImporting(m_AssetData.identifier))
                AddMenuEntry(evt, L10n.Tr("Cancel Import"), true, (_) => m_AssetImporter.CancelImport(m_AssetData.identifier, true));
        }

        void AddMenuEntry(ContextualMenuPopulateEvent evt, string actionName, bool enabled, Action<DropdownMenuAction> action)
        {
            evt.menu.InsertAction(0, actionName, action, enabled ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        void RemoveFromProject(ContextualMenuPopulateEvent evt)
        {
            var enabled = m_AssetDataManager.IsInProject(m_AssetData.identifier) && !m_AssetImporter.IsImporting(m_AssetData.identifier);

            AddMenuEntry(evt, Constants.ContextMenuRemoveFromLibrary, enabled, (_) =>
            {
                m_AssetImporter.RemoveImport(m_AssetData, true);
            });
        }

        void ShowInProject(ContextualMenuPopulateEvent evt)
        {
            var enabled = m_AssetDataManager.IsInProject(m_AssetData.identifier);
            AddMenuEntry(evt, "Show In Project", enabled, (_) =>
            {
                m_AssetImporter.ShowInProject(m_AssetData);
            });
        }

        void ShowInDashboard(ContextualMenuPopulateEvent evt)
        {
            AddMenuEntry(evt, "Show In Dashboard", true, (_) =>
            {
                m_LinksProxy.OpenAssetManagerDashboard(m_AssetData.identifier.projectId, m_AssetData.identifier.assetId);
            });
        }

        private void RefreshHighlight()
        {
            if (m_AssetData == null)
                return;

            var isSelected = m_AssetData.identifier.Equals(m_PageManager.activePage?.selectedAssetId);
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
            if (m_IsLoading) return;
            onClick?.Invoke();
        }
    }
}
