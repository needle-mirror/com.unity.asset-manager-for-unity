using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class MultiAssetDetailsPage : SelectionInspectorPage
    {
        static readonly string k_InspectorScrollviewContainerClassName = "inspector-page-content-container";

        static readonly string k_MultiSelectionUnimportedFoldoutClassName = "multi-selection-unimported-foldout";
        static readonly string k_MultiSelectionUnimportedListViewClassName = "multi-selection-unimported-listview";
        static readonly string k_UnimportedFoldoutTitle = "Unimported";

        static readonly string k_MultiSelectionImportedFoldoutClassName = "multi-selection-imported-foldout";
        static readonly string k_MultiSelectionImportedListViewClassName = "multi-selection-imported-listview";
        static readonly string k_ImportedFoldoutTitle = "Imported";

        static readonly string k_MultiSelectionFoldoutExpandedClassName = "multi-selection-foldout-expanded";
        static readonly string k_BigButtonClassName = "big-button";
        static readonly string k_MultiSelectionRemoveName = "multi-selection-remove-button";
        static readonly string k_InspectorFooterContainerName = "footer-container";

        List<IAssetData> m_SelectedAssetsData = new();
        MultiSelectionFoldout m_UnimportedFoldout;
        MultiSelectionFoldout m_ImportedFoldout;
        Button m_RemoveImportButton;
        VisualElement m_FooterContainer;
        OperationProgressBar m_OperationProgressBar;

        public MultiAssetDetailsPage(IAssetImporter assetImporter, IAssetOperationManager assetOperationManager,
            IStateManager stateManager, IPageManager pageManager, IAssetDataManager assetDataManager,
            IAssetDatabaseProxy assetDatabaseProxy, IProjectOrganizationProvider projectOrganizationProvider,
            ILinksProxy linksProxy, IUnityConnectProxy unityConnectProxy, IProjectIconDownloader projectIconDownloader,
            IPermissionsManager permissionsManager)
            : base(assetImporter, assetOperationManager, stateManager, pageManager, assetDataManager,
                assetDatabaseProxy, projectOrganizationProvider, linksProxy, unityConnectProxy, projectIconDownloader,
                permissionsManager)
        {
            BuildUxmlDocument();
        }

        public override bool IsVisible(int selectedAssetCount)
        {
            return selectedAssetCount > 1;
        }

        protected sealed override void BuildUxmlDocument()
        {
            base.BuildUxmlDocument();

            var container = m_ScrollView.Q<VisualElement>(k_InspectorScrollviewContainerClassName);
            m_UnimportedFoldout = new MultiSelectionFoldout(container, k_MultiSelectionUnimportedFoldoutClassName,
                k_MultiSelectionUnimportedListViewClassName, Constants.ImportActionText,
                ImportUnimportedAssetsAsync, k_UnimportedFoldoutTitle, k_MultiSelectionFoldoutExpandedClassName);
            m_ImportedFoldout = new MultiSelectionFoldout(container, k_MultiSelectionImportedFoldoutClassName,
                k_MultiSelectionImportedListViewClassName, Constants.ReimportActionText,
                ReImportAssetsAsync,k_ImportedFoldoutTitle, k_MultiSelectionFoldoutExpandedClassName);

            // Foldout for non imported assets
            m_UnimportedFoldout.RegisterValueChangedCallback(_ =>
            {
                m_StateManager.MultiSelectionUnimportedFoldoutValue = m_UnimportedFoldout.Expanded;
                RefreshScrollView();
            });
            m_UnimportedFoldout.Expanded = m_StateManager.MultiSelectionUnimportedFoldoutValue;

            // Foldout for imported assets
            m_ImportedFoldout.RegisterValueChangedCallback(_ =>
            {
                m_StateManager.MultiSelectionImportedFoldoutValue = m_ImportedFoldout.Expanded;
                RefreshScrollView();
            });
            m_ImportedFoldout.Expanded = m_StateManager.MultiSelectionImportedFoldoutValue;

            m_FooterContainer = this.Q<VisualElement>(k_InspectorFooterContainerName);
            m_OperationProgressBar = new OperationProgressBar(() =>
            {
                m_AssetImporter.CancelBulkImport(m_SelectedAssetsData.Select(x => x.Identifier).ToList(), true);
            });
            m_FooterContainer.contentContainer.hierarchy.Add(m_OperationProgressBar);

            m_RemoveImportButton = new Button
            {
                text = L10n.Tr(Constants.RemoveAllFromProjectActionText),
                name = k_MultiSelectionRemoveName
            };
            m_RemoveImportButton.AddToClassList(k_BigButtonClassName);
            m_FooterContainer.contentContainer.hierarchy.Add(m_RemoveImportButton);

            m_RemoveImportButton.clicked += RemoveAllFromLocalProject;

            // We need to manually refresh once to make sure the UI is updated when the window is opened.
            if(m_PageManager.ActivePage == null)
                return;

            m_SelectedAssetsData = m_AssetDataManager.GetAssetsData(m_PageManager.ActivePage.SelectedAssets);
            RefreshUI();
        }

        protected override Task SelectAssetDataAsync(List<IAssetData> assetData)
        {
            m_SelectedAssetsData = assetData;
            RefreshUI();

            return Task.CompletedTask;
        }

        protected override void OnOperationProgress(AssetDataOperation operation)
        {
            if(!UIElementsUtils.IsDisplayed(this) || m_SelectedAssetsData == null || !m_SelectedAssetsData.Any() || !m_SelectedAssetsData.Exists(x => x.Identifier.Equals(operation.Identifier)))
                return;

            m_OperationProgressBar.Refresh(operation);

            RefreshUI();
        }

        protected override void OnOperationFinished(AssetDataOperation operation)
        {
            if(!UIElementsUtils.IsDisplayed(this) || m_SelectedAssetsData == null || !m_SelectedAssetsData.Any() || !m_SelectedAssetsData.Exists(x => x.Identifier.Equals(operation.Identifier)))
                return;

            RefreshUI();
        }

        protected override void OnImportedAssetInfoChanged(AssetChangeArgs args)
        {
            if (!UIElementsUtils.IsDisplayed(this))
                return;

            var last = m_SelectedAssetsData[^1];
            foreach (var assetData in m_SelectedAssetsData)
            {
                if (args.Added.Concat(args.Updated).Concat(args.Removed)
                    .Any(a => a.Equals(assetData?.Identifier)))
                {
                    break;
                }

                if (assetData.Equals(last))
                    return;
            }

            // In case of an import, force a full refresh of the displayed information
            TaskUtils.TrackException(SelectAssetDataAsync(m_SelectedAssetsData));
        }

        protected override void OnAssetDataChanged(AssetChangeArgs args)
        {
            RefreshUI();
        }

        protected override void OnCloudServicesReachabilityChanged(bool cloudServiceReachable)
        {
            RefreshUI();
        }

        void RefreshUI()
        {
            if ( !IsVisible(m_SelectedAssetsData.Count))
                return;

            m_TitleLabel.text = L10n.Tr(m_SelectedAssetsData.Count + " " + Constants.AssetsSelectedTitle);
            RefreshFoldoutUI();

            foreach (var assetData in m_SelectedAssetsData)
            {
                var operation = m_AssetOperationManager.GetAssetOperation(assetData.Identifier);
                m_OperationProgressBar.Refresh(operation);
            }

            var removable = m_SelectedAssetsData.Where(x => m_AssetDataManager.IsInProject(x.Identifier)).ToList();
            m_RemoveImportButton.SetEnabled(removable.Count > 0);
            m_RemoveImportButton.text = $"{L10n.Tr(Constants.RemoveAllFromProjectActionText)} ({removable.Count})";
        }

        void RefreshFoldoutUI()
        {
            m_UnimportedFoldout.StartPopulating();
            var unimportedAssets = m_SelectedAssetsData.Where(x => !m_AssetDataManager.IsInProject(x.Identifier)).ToList();

            if (unimportedAssets.Any())
            {
                m_UnimportedFoldout.Populate(null, unimportedAssets);
            }
            else
            {
                m_UnimportedFoldout.Clear();
            }
            m_UnimportedFoldout.StopPopulating();
            m_UnimportedFoldout.RefreshFoldoutStyleBasedOnExpansionStatus();

            m_ImportedFoldout.StartPopulating();
            var importedAssets = m_SelectedAssetsData.Where(x => m_AssetDataManager.IsInProject(x.Identifier)).ToList();

            if (importedAssets.Any())
            {
                m_ImportedFoldout.Populate(null, importedAssets);
            }
            else
            {
                m_ImportedFoldout.Clear();
            }
            m_ImportedFoldout.StopPopulating();
            m_ImportedFoldout.RefreshFoldoutStyleBasedOnExpansionStatus();
        }

        void ImportListAsync(List<IAssetData> assetsData)
        {
            try
            {
                m_AssetImporter.StartImportAsync(assetsData, ImportOperation.ImportType.UpdateToLatest);
            }
            catch (Exception e)
            {
               Debug.LogException(e);
               throw;
            }
        }

        void ImportUnimportedAssetsAsync()
        {
            var unimportedAssets = m_PageManager.ActivePage.SelectedAssets.Where(x => !m_AssetDataManager.IsInProject(x))
                .Select(x => m_AssetDataManager.GetAssetData(x)).ToList();

            AnalyticsSender.SendEvent(unimportedAssets.Count > 1
                ? new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.ImportAll)
                : new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.Import));

            ImportListAsync(unimportedAssets);
        }

        void ReImportAssetsAsync()
        {
            var importedAssets = m_PageManager.ActivePage.SelectedAssets.Where(x => m_AssetDataManager.IsInProject(x))
                .Select(x => m_AssetDataManager.GetAssetData(x)).ToList();

            AnalyticsSender.SendEvent(importedAssets.Count > 1
                ? new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.ReImportAll)
                : new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.Reimport));

            ImportListAsync(importedAssets);
        }

        void RemoveAllFromLocalProject()
        {
            var importedAssets = m_PageManager.ActivePage.SelectedAssets.Where(x => m_AssetDataManager.IsInProject(x)).ToList();

            AnalyticsSender.SendEvent(importedAssets.Count > 1
                ? new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.RemoveAll)
                : new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.Remove));

            try
            {
                m_AssetImporter.RemoveBulkImport(importedAssets, true);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
        }
    }
}