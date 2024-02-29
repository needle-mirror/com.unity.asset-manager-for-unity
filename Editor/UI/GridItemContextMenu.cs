using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class GridItemContextMenu
    {
        readonly IGridItem m_GridItem;
        readonly IAssetDataManager m_AssetDataManager;
        readonly IAssetImporter m_AssetImporter;
        readonly ILinksProxy m_LinksProxy;

        bool IsImporting => m_AssetImporter.IsImporting(m_GridItem.AssetData.identifier);
        bool IsInProject => m_AssetDataManager.IsInProject(m_GridItem.AssetData.identifier);

        public GridItemContextMenu(IGridItem gridItem, IAssetDataManager assetDataManager, IAssetImporter assetImporter, ILinksProxy linksProxy)
        {
            m_GridItem = gridItem;
            m_AssetDataManager = assetDataManager;
            m_AssetImporter = assetImporter;
            m_LinksProxy = linksProxy;
        }

        public void SetupContextMenuEntries(ContextualMenuPopulateEvent evt)
        {
            ClearMenuEntries(evt);
            RemoveFromProjectEntry(evt);
            ShowInProjectEntry(evt);
            ShowInDashboardEntry(evt);
            ImportEntry(evt);
            CancelImportEntry(evt);
        }

        static void ClearMenuEntries(ContextualMenuPopulateEvent evt)
        {
            for (int i = 0; i < evt.menu.MenuItems().Count; i++)
            {
                evt.menu.MenuItems().RemoveAt(0);
            }
        }

        static void AddMenuEntry(ContextualMenuPopulateEvent evt, string actionName, bool enabled, Action<DropdownMenuAction> action)
        {
            evt.menu.InsertAction(0, actionName, action, enabled ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        void ImportEntry(ContextualMenuPopulateEvent evt)
        {
            if (IsImporting)
                return;

            var text = !IsInProject ? Constants.ImportActionText : Constants.ReimportActionText;

            AddMenuEntry(evt, text, true,
                (_) =>
                {
                    m_AssetImporter.StartImportAsync(m_GridItem.AssetData, ImportAction.ContextMenu);
                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(!IsInProject ? GridContextMenuItemSelectedEvent.ContextMenuItemType.Import : GridContextMenuItemSelectedEvent.ContextMenuItemType.Reimport));
                });
        }

        void CancelImportEntry(ContextualMenuPopulateEvent evt)
        {
            if (!IsImporting)
                return;

            AddMenuEntry(evt, L10n.Tr(Constants.CancelImportActionText), true,
                (_) =>
                {
                    m_AssetImporter.CancelImport(m_GridItem.AssetData.identifier, true);
                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent.ContextMenuItemType.CancelImport));
                });
        }

        void RemoveFromProjectEntry(ContextualMenuPopulateEvent evt)
        {
            if (!IsInProject || IsImporting)
                return;

            AddMenuEntry(evt, Constants.RemoveFromProjectActionText, true,
                (_) =>
                {
                    m_AssetImporter.RemoveImport(m_GridItem.AssetData.identifier, true);
                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent.ContextMenuItemType.Remove));
                });
        }

        void ShowInProjectEntry(ContextualMenuPopulateEvent evt)
        {
            if (!IsInProject || IsImporting)
                return;

            AddMenuEntry(evt, Constants.ShowInProjectActionText, true,
                (_) =>
                {
                    m_AssetImporter.ShowInProject(m_GridItem.AssetData.identifier);
                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent.ContextMenuItemType.ShowInProject));
                });
        }

        void ShowInDashboardEntry(ContextualMenuPopulateEvent evt)
        {
            AddMenuEntry(evt, Constants.ShowInDashboardActionText, true,
                (_) =>
                {
                    var identifier = m_GridItem.AssetData.identifier;
                    m_LinksProxy.OpenAssetManagerDashboard(identifier);
                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent.ContextMenuItemType.ShowInDashboard));
                });
        }
    }
}
