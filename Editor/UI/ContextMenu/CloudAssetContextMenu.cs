using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class CloudAssetContextMenu : AssetContextMenu
    {
        public CloudAssetContextMenu(IAssetDataManager assetDataManager, IAssetImporter assetImporter,
            ILinksProxy linksProxy, IAssetDatabaseProxy assetDatabaseProxy) : base(assetDataManager, assetImporter,
            linksProxy, assetDatabaseProxy)
        {
        }

        bool IsImporting => m_AssetImporter.IsImporting(TargetAssetData.identifier);
        bool IsInProject => m_AssetDataManager.IsInProject(TargetAssetData.identifier);

        public override void SetupContextMenuEntries(ContextualMenuPopulateEvent evt)
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

        void ImportEntry(ContextualMenuPopulateEvent evt)
        {
            if (IsImporting)
                return;

            var text = !IsInProject ? Constants.ImportActionText : Constants.ReimportActionText;

            AddMenuEntry(evt, text, true,
                (_) =>
                {
                    m_AssetImporter.StartImportAsync(TargetAssetData);
                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(!IsInProject
                        ? GridContextMenuItemSelectedEvent.ContextMenuItemType.Import
                        : GridContextMenuItemSelectedEvent.ContextMenuItemType.Reimport));
                });
        }

        void CancelImportEntry(ContextualMenuPopulateEvent evt)
        {
            if (!IsImporting)
                return;

            AddMenuEntry(evt, L10n.Tr(Constants.CancelImportActionText), true,
                (_) =>
                {
                    m_AssetImporter.CancelImport(TargetAssetData.identifier, true);
                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent
                        .ContextMenuItemType.CancelImport));
                });
        }

        void RemoveFromProjectEntry(ContextualMenuPopulateEvent evt)
        {
            if (!IsInProject || IsImporting)
                return;

            AddMenuEntry(evt, Constants.RemoveFromProjectActionText, true,
                (_) =>
                {
                    m_AssetImporter.RemoveImport(TargetAssetData.identifier, true);
                    AnalyticsSender.SendEvent(
                        new GridContextMenuItemSelectedEvent(
                            GridContextMenuItemSelectedEvent.ContextMenuItemType.Remove));
                });
        }

        void ShowInProjectEntry(ContextualMenuPopulateEvent evt)
        {
            if (!IsInProject || IsImporting)
                return;

            AddMenuEntry(evt, Constants.ShowInProjectActionText, true,
                (_) =>
                {
                    m_AssetImporter.ShowInProject(TargetAssetData.identifier);
                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent
                        .ContextMenuItemType.ShowInProject));
                });
        }

        void ShowInDashboardEntry(ContextualMenuPopulateEvent evt)
        {
            AddMenuEntry(evt, Constants.ShowInDashboardActionText, true,
                (_) =>
                {
                    var identifier = TargetAssetData.identifier;
                    m_LinksProxy.OpenAssetManagerDashboard(identifier);
                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent
                        .ContextMenuItemType.ShowInDashboard));
                });
        }
    }
}