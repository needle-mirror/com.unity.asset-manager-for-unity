using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
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
            TaskUtils.TrackException(SetupContextMenuEntriesAsync(evt));
        }

        async Task SetupContextMenuEntriesAsync(ContextualMenuPopulateEvent evt)
        {
            ClearMenuEntries(evt);
            UpdateAllToLatest(evt);
            RemoveFromProjectEntry(evt);
            ShowInProjectEntry(evt);
            ShowInDashboardEntry(evt);
            await ImportEntry(evt);
            CancelImportEntry(evt);
        }

        static void ClearMenuEntries(ContextualMenuPopulateEvent evt)
        {
            for (var i = 0; i < evt.menu.MenuItems().Count; i++)
            {
                evt.menu.MenuItems().RemoveAt(0);
            }
        }

        async Task ImportEntry(ContextualMenuPopulateEvent evt)
        {
            if (IsImporting || !m_UnityConnectProxy.AreCloudServicesReachable)
                return;

            var permissionsManager = ServicesContainer.instance.Resolve<PermissionsManager>();
            var importPermission = await permissionsManager.CheckPermissionAsync(TargetAssetData.Identifier.OrganizationId, TargetAssetData.Identifier.ProjectId, Constants.ImportPermission);

            var enabled = UIEnabledStates.HasPermissions.GetFlag(importPermission);
            enabled |= UIEnabledStates.ServicesReachable.GetFlag(m_UnityConnectProxy.AreCloudServicesReachable);
            enabled |= UIEnabledStates.IsImporting.GetFlag(IsImporting);
            enabled |= UIEnabledStates.CanImport.GetFlag(true); // We unfortunately don't have a way to check instantly if the asset is not empty, so we need to assume it is.

            var selectedAssetData = m_PageManager.ActivePage.SelectedAssets.Select(x => m_AssetDataManager.GetAssetData(x)).ToList();
            if (selectedAssetData.Count > 1 && selectedAssetData.Exists(ad => ad.Identifier.AssetId == TargetAssetData.Identifier.AssetId))
            {
                ImportEntryMultiple(evt, selectedAssetData, enabled);
            }
            else if (!selectedAssetData.Any() || selectedAssetData.First().Identifier.AssetId == TargetAssetData.Identifier.AssetId)
            {
               ImportEntrySingle(evt, enabled);
            }
        }

        void ImportEntrySingle(ContextualMenuPopulateEvent evt, UIEnabledStates enabled)
        {
            var status = AssetDataStatus.GetIStatusFromAssetDataStatusType(TargetAssetData?.PreviewStatus?.FirstOrDefault());
            enabled |= UIEnabledStates.ValidStatus.GetFlag(status == null || !string.IsNullOrEmpty(status.ActionText));

            var text = AssetDetailsPageExtensions.GetImportButtonLabel(null, status);

            AddMenuEntry(evt, text, AssetDetailsPageExtensions.IsImportAvailable(enabled),
                _ =>
                {
                    m_AssetImporter.StartImportAsync(new List<BaseAssetData> {TargetAssetData}, ImportOperation.ImportType.UpdateToLatest);
                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(!IsInProject
                        ? GridContextMenuItemSelectedEvent.ContextMenuItemType.Import
                        : GridContextMenuItemSelectedEvent.ContextMenuItemType.Reimport));
                });
        }

        void ImportEntryMultiple(ContextualMenuPopulateEvent evt, List<BaseAssetData> selectedAssetData, UIEnabledStates enabled)
        {
            var isContainedInvalidStatus = false;
            foreach (var assetData in selectedAssetData)
            {
                var status = AssetDataStatus.GetIStatusFromAssetDataStatusType(assetData?.PreviewStatus?.FirstOrDefault());
                if(!(status == null || !string.IsNullOrEmpty(status.ActionText)))
                {
                    isContainedInvalidStatus = true;
                    break;
                }
            }
            enabled |= UIEnabledStates.ValidStatus.GetFlag(!isContainedInvalidStatus);

            AddMenuEntry(evt, L10n.Tr(Constants.ImportAllSelectedActionText), AssetDetailsPageExtensions.IsImportAvailable(enabled),
                _ =>
                {
                    m_AssetImporter.StartImportAsync(selectedAssetData, ImportOperation.ImportType.UpdateToLatest);
                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent
                        .ContextMenuItemType.ImportAll));
                });
        }

        void CancelImportEntry(ContextualMenuPopulateEvent evt)
        {
            if (!IsImporting)
                return;

            AddMenuEntry(evt, L10n.Tr(AssetManagerCoreConstants.CancelImportActionText), true,
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
                selectedAssetData.TrueForAll(x => m_AssetDataManager.IsInProject(x.Identifier)))
            {
                AddMenuEntry(evt, L10n.Tr(Constants.RemoveFromProjectAllSelectedActionText), true,
                    _ =>
                    {
                        m_AssetImporter.RemoveImports(selectedAssetData.Select(x => x.Identifier).ToList(), true);
                        AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent
                            .ContextMenuItemType.RemoveAll));
                    });
            }
            else if (!selectedAssetData.Any() || selectedAssetData[0].Identifier.AssetId == TargetAssetData.Identifier.AssetId)
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

        void UpdateAllToLatest(ContextualMenuPopulateEvent evt)
        {
            if (!m_UnityConnectProxy.AreCloudServicesReachable)
                return;

            var selectedAssetData = m_PageManager.ActivePage.SelectedAssets.Select(x => m_AssetDataManager.GetAssetData(x)).ToList();
            if (selectedAssetData.Count == 1)
                return;

            var enabled = m_AssetDataManager.ImportedAssetInfos.Any() && !IsImporting;
            AddMenuEntry(evt, selectedAssetData.Count > 1 ? Constants.UpdateSelectedToLatestActionText : Constants.UpdateAllToLatestActionText, enabled,
                (_) =>
                {
                    if (selectedAssetData.Count == 0)
                    {
                        ProjectInfo selectedProject = null;
                        CollectionInfo selectedCollection = null;

                        if (m_PageManager.ActivePage is CollectionPage)
                        {
                            var projectOrganizationProvider =
                                ServicesContainer.instance.Resolve<IProjectOrganizationProvider>();
                            selectedProject = projectOrganizationProvider.SelectedProject;
                            selectedCollection = projectOrganizationProvider.SelectedCollection;
                        }

                        TaskUtils.TrackException(m_AssetImporter.UpdateAllToLatestAsync(selectedProject, selectedCollection, CancellationToken.None));
                    }
                    else
                    {
                        TaskUtils.TrackException(m_AssetImporter.UpdateAllToLatestAsync(selectedAssetData, CancellationToken.None));
                    }

                    AnalyticsSender.SendEvent(new GridContextMenuItemSelectedEvent(GridContextMenuItemSelectedEvent
                        .ContextMenuItemType.UpdateAllToLatest));
                });
        }
    }
}
