using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class CloudAssetContextMenu : AssetContextMenu
    {
        public CloudAssetContextMenu(IUnityConnectProxy unityConnectProxy, IAssetDataManager assetDataManager, IAssetImporter assetImporter,
            ILinksProxy linksProxy, IAssetDatabaseProxy assetDatabaseProxy, IPageManager pageManager) : base(unityConnectProxy, assetDataManager, assetImporter,
            linksProxy, assetDatabaseProxy, pageManager) { }

        bool IsImporting => m_AssetImporter.IsImporting(TargetAssetData.Identifier);
        bool IsInProject => m_AssetDataManager.IsInProject(TargetAssetData.Identifier);

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
            for (var i = 0; i < evt.menu.MenuItems().Count; i++)
            {
                evt.menu.MenuItems().RemoveAt(0);
            }
        }

        void ImportEntry(ContextualMenuPopulateEvent evt)
        {
            if (IsImporting || !m_UnityConnectProxy.AreCloudServicesReachable)
                return;

            var selectedAssetData = m_PageManager.ActivePage.SelectedAssets.Select(x => m_AssetDataManager.GetAssetData(x)).ToList();
            if (selectedAssetData.Count > 1 && selectedAssetData.Any(ad => ad.Identifier.AssetId == TargetAssetData.Identifier.AssetId))
            {
                AddMenuEntry(evt, L10n.Tr(Constants.ImportAllSelectedActionText), true,
                    _ =>
                    {
                        m_AssetImporter.StartImportAsync(selectedAssetData, ImportOperation.ImportType.UpdateToLatest);
                        AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent
                            .ContextMenuItemType.ImportAll));
                    });
            }
            else if (!selectedAssetData.Any() || selectedAssetData.First().Identifier.AssetId == TargetAssetData.Identifier.AssetId)
            {
                var status = TargetAssetData.PreviewStatus.FirstOrDefault();

                var text = AssetDetailsPageExtensions.GetImportButtonLabel(null, status);
                var permissionsManager = ServicesContainer.instance.Resolve<PermissionsManager>();
                var enabled = UIEnabledStates.HasPermissions.GetFlag(permissionsManager.CheckPermission(Constants.ImportPermission));
                enabled |= UIEnabledStates.ServicesReachable.GetFlag(m_UnityConnectProxy.AreCloudServicesReachable);
                enabled |= UIEnabledStates.ValidStatus.GetFlag(status == null || !string.IsNullOrEmpty(status.ActionText));
                enabled |= UIEnabledStates.IsImporting.GetFlag(IsImporting);

                AddMenuEntry(evt, text, AssetDetailsPageExtensions.IsImportAvailable(enabled),
                    _ =>
                    {
                        m_AssetImporter.StartImportAsync(new List<IAssetData>() {TargetAssetData}, ImportOperation.ImportType.UpdateToLatest);
                        AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(!IsInProject
                            ? GridContextMenuItemSelectedEvent.ContextMenuItemType.Import
                            : GridContextMenuItemSelectedEvent.ContextMenuItemType.Reimport));
                    });
            }
        }

        void CancelImportEntry(ContextualMenuPopulateEvent evt)
        {
            if (!IsImporting)
                return;

            AddMenuEntry(evt, L10n.Tr(Constants.CancelImportActionText), true,
                _ =>
                {
                    m_AssetImporter.CancelImport(TargetAssetData.Identifier, true);
                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent
                        .ContextMenuItemType.CancelImport));
                });
        }

        void RemoveFromProjectEntry(ContextualMenuPopulateEvent evt)
        {
            if (!IsInProject || IsImporting)
                return;

            var selectedAssetData = m_PageManager.ActivePage.SelectedAssets.Select(x => m_AssetDataManager.GetAssetData(x)).ToList();
            if (selectedAssetData.Count > 1 &&
                selectedAssetData.All(x => m_AssetDataManager.IsInProject(x.Identifier)))
            {
                AddMenuEntry(evt, L10n.Tr(Constants.RemoveFromProjectAllSelectedActionText), true,
                    _ =>
                    {
                        m_AssetImporter.RemoveBulkImport(selectedAssetData.Select(x => x.Identifier).ToList(), true);
                        AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent
                            .ContextMenuItemType.RemoveAll));
                    });
            }
            else if(!selectedAssetData.Any() || selectedAssetData.First().Identifier.AssetId == TargetAssetData.Identifier.AssetId)
            {
                AddMenuEntry(evt, L10n.Tr(Constants.RemoveFromProjectActionText), true,
                    _ =>
                    {
                        m_AssetImporter.RemoveImport(TargetAssetData.Identifier, true);
                        AnalyticsSender.SendEvent(
                            new GridContextMenuItemSelectedEvent(
                                GridContextMenuItemSelectedEvent.ContextMenuItemType.Remove));
                    });
            }
        }

        void ShowInProjectEntry(ContextualMenuPopulateEvent evt)
        {
            if (!IsInProject || IsImporting)
                return;

            AddMenuEntry(evt, Constants.ShowInProjectActionText, true,
                _ =>
                {
                    m_AssetImporter.ShowInProject(TargetAssetData.Identifier);
                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent
                        .ContextMenuItemType.ShowInProject));
                });
        }

        void ShowInDashboardEntry(ContextualMenuPopulateEvent evt)
        {
            if (!m_UnityConnectProxy.AreCloudServicesReachable)
                return;

            AddMenuEntry(evt, Constants.ShowInDashboardActionText, true,
                _ =>
                {
                    var identifier = TargetAssetData.Identifier;
                    m_LinksProxy.OpenAssetManagerDashboard(identifier);
                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent
                        .ContextMenuItemType.ShowInDashboard));
                });
        }
    }
}
