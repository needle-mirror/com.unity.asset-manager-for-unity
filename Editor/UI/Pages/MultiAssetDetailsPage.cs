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

        static readonly string k_UnimportedFoldoutClassName = "multi-selection-unimported-foldout";
        static readonly string k_ImportedFoldoutClassName = "multi-selection-imported-foldout";
        static readonly string k_UploadIgnoredFoldoutClassName = "multi-selection-upload-ignored-foldout";
        static readonly string k_UploadIncludedFoldoutClassName = "multi-selection-upload-included-foldout";

        static readonly string k_UnimportedListViewClassName = "multi-selection-unimported-listview";
        static readonly string k_ImportedListViewClassName = "multi-selection-imported-listview";
        static readonly string k_UploadIgnoredListViewClassName = "multi-selection-upload-ignored-listview";
        static readonly string k_UploadIncludedListViewClassName = "multi-selection-upload-included-listview";

        static readonly string k_UnimportedFoldoutTitle = "Unimported";
        static readonly string k_ImportedFoldoutTitle = "Imported";
        static readonly string k_UploadIgnoredFoldoutTitle = "Ignored";
        static readonly string k_UploadIncludedFoldoutTitle = "Included";

        static readonly string k_MultiSelectionFoldoutExpandedClassName = "multi-selection-foldout-expanded";

        static readonly string k_BigButtonClassName = "big-button";
        static readonly string k_MultiSelectionRemoveName = "multi-selection-remove-button";
        static readonly string k_InspectorFooterContainerName = "footer-container";

        List<IAssetData> m_SelectedAssetsData = new();
        public enum FoldoutName
        {
            Unimported = 0,
            Imported = 1,
            UploadIgnored = 2,
            UploadIncluded = 3
        }
        readonly Dictionary<FoldoutName, MultiSelectionFoldout> m_Foldouts = new();

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

            m_Foldouts[FoldoutName.Unimported] = new MultiSelectionFoldout(container, k_UnimportedFoldoutClassName,
                k_UnimportedListViewClassName, Constants.ImportActionText,
                ImportUnimportedAssetsAsync, k_UnimportedFoldoutTitle, k_MultiSelectionFoldoutExpandedClassName);

            m_Foldouts[FoldoutName.Imported] = new MultiSelectionFoldout(container, k_ImportedFoldoutClassName,
                k_ImportedListViewClassName, Constants.ReimportActionText,
                ReImportAssetsAsync,k_ImportedFoldoutTitle, k_MultiSelectionFoldoutExpandedClassName);

            m_Foldouts[FoldoutName.UploadIgnored] = new MultiSelectionFoldout(container, k_UploadIgnoredFoldoutClassName,
                k_UploadIgnoredListViewClassName, Constants.IncludeAll,
                IncludeUploadAssets, k_UploadIgnoredFoldoutTitle, k_MultiSelectionFoldoutExpandedClassName);

            m_Foldouts[FoldoutName.UploadIncluded] = new MultiSelectionFoldout(container, k_UploadIncludedFoldoutClassName,
                k_UploadIncludedListViewClassName, Constants.IgnoreAll,
                IgnoreUploadAssets, k_UploadIncludedFoldoutTitle, k_MultiSelectionFoldoutExpandedClassName);

            foreach (var foldout in m_Foldouts)
            {
                foldout.Value.RegisterValueChangedCallback(_ =>
                {
                    m_StateManager.MultiSelectionFoldoutsValues[(int)foldout.Key] = foldout.Value.Expanded;
                    RefreshScrollView();
                });
                foldout.Value.Expanded = m_StateManager.MultiSelectionFoldoutsValues[(int)foldout.Key];
            }

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
            // Check if assetData is a subset of m_SelectedAssetsData
            if (assetData.Count < m_SelectedAssetsData.Count && !assetData.Except(m_SelectedAssetsData).Any())
            {
                RemoveItemsFromFoldouts(m_SelectedAssetsData.Except(assetData));
                m_SelectedAssetsData = assetData;
                RefreshTitleAndButtons();
            }
            else
            {
                m_SelectedAssetsData = assetData;
                RefreshUI();
            }

            RefreshScrollView();
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

            if (m_SelectedAssetsData == null || !m_SelectedAssetsData.Any())
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
            m_SelectedAssetsData = m_AssetDataManager.GetAssetsData(m_PageManager.ActivePage.SelectedAssets);
            RefreshUI();
        }

        protected override void OnCloudServicesReachabilityChanged(bool cloudServiceReachable)
        {
            RefreshUI();
        }

        void RefreshTitleAndButtons()
        {
            // Refresh Title
            m_TitleLabel.text = L10n.Tr(m_SelectedAssetsData.Count + " " + Constants.AssetsSelectedTitle);

            // Refresh RemoveImportButton
            var removable = m_SelectedAssetsData.Where(x => m_AssetDataManager.IsInProject(x.Identifier)).ToList();
            m_RemoveImportButton.SetEnabled(removable.Count > 0);
            m_RemoveImportButton.text = $"{L10n.Tr(Constants.RemoveAllFromProjectActionText)} ({removable.Count})";

            // Refresh ProgressBar
            bool atLeastOneProcess = false;
            foreach (var assetData in m_SelectedAssetsData)
            {
                var operation = m_AssetOperationManager.GetAssetOperation(assetData.Identifier);
                if (operation != null)
                {
                    atLeastOneProcess = true;
                    m_OperationProgressBar.Refresh(operation);
                }
            }
            if (!atLeastOneProcess)
            {
                UIElementsUtils.Hide(m_OperationProgressBar);
            }
        }

        void RefreshUI()
        {
            if ( !IsVisible(m_SelectedAssetsData.Count))
                return;

            RefreshFoldoutUI();
            RefreshTitleAndButtons();
        }

        void RefreshFoldoutUI()
        {
            // Check which page is displayed
            if(m_PageManager.ActivePage is UploadPage)
            {
                RefreshUploadPageFoldoutUI();
            }
            else
            {
                RefreshAssetPageFoldoutUI();
            }
        }

        void ClearFoldout(FoldoutName foldoutName)
        {
            m_Foldouts[foldoutName].StartPopulating();
            m_Foldouts[foldoutName].Clear();
            m_Foldouts[foldoutName].StopPopulating();
            m_Foldouts[foldoutName].RefreshFoldoutStyleBasedOnExpansionStatus();
        }

        void PopulateFoldout(FoldoutName foldoutName, IEnumerable<IAssetData> items)
        {
            var haveEmptyAsset = items.Any(i => !i.SourceFiles.Any());
            m_Foldouts[foldoutName].SetButtonEnable(!haveEmptyAsset);
            m_Foldouts[foldoutName].StartPopulating();
            var assetDatas = items.ToList();
            if (assetDatas.Any())
            {
                m_Foldouts[foldoutName].Populate(null, assetDatas);
            }
            else
            {
                m_Foldouts[foldoutName].Clear();
            }
            m_Foldouts[foldoutName].StopPopulating();
            m_Foldouts[foldoutName].RefreshFoldoutStyleBasedOnExpansionStatus();
        }

        void RemoveItemsFromFoldouts(IEnumerable<IAssetData> items)
        {
            foreach (var foldout in m_Foldouts)
            {
                m_Foldouts[foldout.Key].RemoveItems(items);
            }
        }

        void RefreshAssetPageFoldoutUI()
        {
            UIElementsUtils.SetDisplay(m_RemoveImportButton, true);

            ClearFoldout(FoldoutName.UploadIgnored);
            ClearFoldout(FoldoutName.UploadIncluded);

            PopulateFoldout(FoldoutName.Unimported, m_SelectedAssetsData.Where(x => !m_AssetDataManager.IsInProject(x.Identifier)));
            PopulateFoldout(FoldoutName.Imported, m_SelectedAssetsData.Where(x => m_AssetDataManager.IsInProject(x.Identifier)));
        }

        void RefreshUploadPageFoldoutUI()
        {
            UIElementsUtils.SetDisplay(m_RemoveImportButton, false);

            ClearFoldout(FoldoutName.Unimported);
            ClearFoldout(FoldoutName.Imported);

            var uploadAssetDatas = new List<UploadAssetData>();
            if(m_SelectedAssetsData.Exists(x => x is UploadAssetData))
            {
                uploadAssetDatas = m_SelectedAssetsData.Cast<UploadAssetData>().ToList();
            }

            PopulateFoldout(FoldoutName.UploadIgnored, uploadAssetDatas.Where(x => x.IsIgnored));
            PopulateFoldout(FoldoutName.UploadIncluded,  uploadAssetDatas.Where(x => !x.IsIgnored));
        }

        void IgnoreUploadAssets()
        {
            foreach (var assetData in m_SelectedAssetsData.Cast<UploadAssetData>().Where(x => !x.IsIgnored).ToList())
            {
                m_PageManager.ActivePage.ToggleAsset(assetData, assetData.IsIgnored);
            }
        }

        void IncludeUploadAssets()
        {
            foreach (var assetData in m_SelectedAssetsData.Cast<UploadAssetData>().Where(x => x.IsIgnored).ToList())
            {
                m_PageManager.ActivePage.ToggleAsset(assetData, assetData.IsIgnored);
            }
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
