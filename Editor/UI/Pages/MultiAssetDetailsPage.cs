using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Upload.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class MultiAssetDetailsPage : SelectionInspectorPage
    {
        static readonly string k_InspectorScrollviewContainerClassName = "inspector-page-content-container";

        static readonly string k_MultiSelectionFoldoutExpandedClassName = "multi-selection-foldout-expanded";

        static readonly string k_BigButtonClassName = "big-button";
        static readonly string k_MultiSelectionRemoveName = "multi-selection-remove-button";
        static readonly string k_InspectorFooterContainerName = "footer-container";

        static readonly string k_UnimportedFoldoutClassName = "multi-selection-unimported-foldout";
        static readonly string k_ImportedFoldoutClassName = "multi-selection-imported-foldout";
        static readonly string k_UploadRemovedFoldoutClassName = "multi-selection-upload-removed-foldout";
        static readonly string k_UploadIgnoredFoldoutClassName = "multi-selection-upload-ignored-foldout";
        static readonly string k_UploadIncludedFoldoutClassName = "multi-selection-upload-included-foldout";

        static readonly string k_UnimportedFoldoutTitle = "Unimported";
        static readonly string k_ImportedFoldoutTitle = "Imported";
        static readonly string k_UploadRemovedFoldoutTitle = "Included";
        static readonly string k_UploadIgnoredFoldoutTitle = "Ignored Dependencies";
        static readonly string k_UploadIncludedFoldoutTitle = "Included Dependencies";

        readonly AssetDataSelection m_SelectedAssetsData = new();

        public enum FoldoutName
        {
            Unimported = 0,
            Imported = 1,
            UploadIgnored = 2,
            UploadIncluded = 3,
            UploadRemoved = 4
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

            m_SelectedAssetsData.AssetDataChanged += OnAssetDataEvent;
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
                Constants.ImportActionText, ImportUnimportedAssetsAsync, k_UnimportedFoldoutTitle,
                k_MultiSelectionFoldoutExpandedClassName);

            m_Foldouts[FoldoutName.Imported] = new MultiSelectionFoldout(container, k_ImportedFoldoutClassName,
                Constants.ReimportActionText, ReImportAssetsAsync, k_ImportedFoldoutTitle,
                k_MultiSelectionFoldoutExpandedClassName);

            m_Foldouts[FoldoutName.UploadRemoved] = new MultiSelectionFoldout(container, k_UploadRemovedFoldoutClassName,
                Constants.RemoveAll, RemoveUploadAssets, k_UploadRemovedFoldoutTitle,
                k_MultiSelectionFoldoutExpandedClassName);

            m_Foldouts[FoldoutName.UploadIgnored] = new MultiSelectionFoldout(container, k_UploadIgnoredFoldoutClassName,
                Constants.IncludeAll, IncludeUploadAssets, k_UploadIgnoredFoldoutTitle,
                k_MultiSelectionFoldoutExpandedClassName);

            m_Foldouts[FoldoutName.UploadIncluded] = new MultiSelectionFoldout(container, k_UploadIncludedFoldoutClassName,
                Constants.IgnoreAll, IgnoreUploadAssets, k_UploadIncludedFoldoutTitle,
                k_MultiSelectionFoldoutExpandedClassName);

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
            m_OperationProgressBar = new OperationProgressBar(CancelOrClearImport);
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

            m_SelectedAssetsData.Selection = m_AssetDataManager.GetAssetsData(m_PageManager.ActivePage.SelectedAssets);
            RefreshUI();
        }

        void CancelOrClearImport()
        {
            var operations = new List<AssetDataOperation>();
            foreach (var id in m_SelectedAssetsData.Selection.Select(x => x.Identifier))
            {
                var operation = m_AssetOperationManager.GetAssetOperation(id);
                Utilities.DevAssert(operation != null, $"Operation for asset {id} not found");
                if (operation != null)
                {
                    operations.Add(operation);
                }
            }

            if (operations.Exists(o => o.Status == OperationStatus.InProgress))
            {
                m_AssetImporter.CancelBulkImport(m_SelectedAssetsData.Selection.Select(x => x.Identifier).ToList(), true);
            }
            else
            {
                m_AssetOperationManager.ClearFinishedOperations();
            }
        }

        protected override Task SelectAssetDataAsync(IReadOnlyCollection<BaseAssetData> assetData)
        {
            if (assetData == null || assetData.Count == 0)
            {
                m_SelectedAssetsData.Clear();
                return Task.CompletedTask;
            }

            // Check if assetData is a subset of m_SelectedAssetsData
            if (assetData.Count < m_SelectedAssetsData.Selection.Count && !assetData.Except(m_SelectedAssetsData.Selection).Any())
            {
                RemoveItemsFromFoldouts(m_SelectedAssetsData.Selection.Except(assetData));
                m_SelectedAssetsData.Selection = assetData;
                RefreshTitleAndButtons();
            }
            else
            {
                m_SelectedAssetsData.Selection = assetData;
                RefreshUI();
            }

            RefreshScrollView();
            return Task.CompletedTask;
        }

        protected override void OnOperationProgress(AssetDataOperation operation)
        {
            if(!UIElementsUtils.IsDisplayed(this) || m_SelectedAssetsData == null || !m_SelectedAssetsData.Selection.Any() || !m_SelectedAssetsData.Exists(x => x.Identifier.Equals(operation.Identifier)))
                return;

            m_OperationProgressBar.Refresh(operation);

            RefreshUI();
        }

        protected override void OnOperationFinished(AssetDataOperation operation)
        {
            if (!UIElementsUtils.IsDisplayed(this) || m_SelectedAssetsData == null || !m_SelectedAssetsData.Selection.Any() || !m_SelectedAssetsData.Exists(x => x.Identifier.Equals(operation.Identifier)))
                return;

            RefreshUI();
        }

        protected override void OnImportedAssetInfoChanged(AssetChangeArgs args)
        {
            if (!UIElementsUtils.IsDisplayed(this))
                return;

            if (m_SelectedAssetsData == null || !m_SelectedAssetsData.Selection.Any())
                return;

            var last = m_SelectedAssetsData.Selection.Last();
            foreach (var assetData in m_SelectedAssetsData.Selection)
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
            TaskUtils.TrackException(SelectAssetDataAsync(m_SelectedAssetsData.Selection));
        }

        protected override void OnAssetDataChanged(AssetChangeArgs args)
        {
            m_SelectedAssetsData.Selection = m_AssetDataManager.GetAssetsData(m_PageManager.ActivePage.SelectedAssets);
            RefreshUI();
        }

        protected override void OnCloudServicesReachabilityChanged(bool cloudServiceReachable)
        {
            RefreshUI();
        }

        void OnAssetDataEvent(BaseAssetData assetData, AssetDataEventType eventType)
        {
            RefreshUI();
        }

        void RefreshTitleAndButtons()
        {
            // Refresh Title
            m_TitleLabel.text = L10n.Tr(m_SelectedAssetsData.Selection.Count + " " + Constants.AssetsSelectedTitle);

            // Refresh RemoveImportButton
            var removable = m_SelectedAssetsData.Selection.Where(x => m_AssetDataManager.IsInProject(x.Identifier)).ToList();
            m_RemoveImportButton.SetEnabled(removable.Count > 0);
            m_RemoveImportButton.text = $"{L10n.Tr(Constants.RemoveAllFromProjectActionText)} ({removable.Count})";

            // Refresh ProgressBar
            bool atLeastOneProcess = false;
            foreach (var assetData in m_SelectedAssetsData.Selection)
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
            if (!IsVisible(m_SelectedAssetsData.Selection.Count))
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

        void PopulateFoldout(FoldoutName foldoutName, IEnumerable<BaseAssetData> items)
        {
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

        void RemoveItemsFromFoldouts(IEnumerable<BaseAssetData> items)
        {
            foreach (var foldout in m_Foldouts)
            {
                m_Foldouts[foldout.Key].RemoveItems(items);
            }
        }

        void RefreshAssetPageFoldoutUI()
        {
            UIElementsUtils.SetDisplay(m_RemoveImportButton, true);

            ClearFoldout(FoldoutName.UploadRemoved);
            ClearFoldout(FoldoutName.UploadIgnored);
            ClearFoldout(FoldoutName.UploadIncluded);

            PopulateFoldout(FoldoutName.Unimported, m_SelectedAssetsData.Selection.Where(x => !m_AssetDataManager.IsInProject(x.Identifier)));
            PopulateFoldout(FoldoutName.Imported, m_SelectedAssetsData.Selection.Where(x => m_AssetDataManager.IsInProject(x.Identifier)));
        }

        void RefreshUploadPageFoldoutUI()
        {
            UIElementsUtils.SetDisplay(m_RemoveImportButton, false);

            ClearFoldout(FoldoutName.Unimported);
            ClearFoldout(FoldoutName.Imported);

            var uploadAssetData = new List<UploadAssetData>();
            if (m_SelectedAssetsData.Exists(x => x is UploadAssetData))
            {
                uploadAssetData = m_SelectedAssetsData.Selection.Cast<UploadAssetData>().ToList();
            }

            PopulateFoldout(FoldoutName.UploadRemoved, uploadAssetData.Where(x => x.CanBeRemoved));
            PopulateFoldout(FoldoutName.UploadIgnored, uploadAssetData.Where(x => x.CanBeIgnored && x.IsIgnored));
            PopulateFoldout(FoldoutName.UploadIncluded, uploadAssetData.Where(x => x.CanBeIgnored && !x.IsIgnored));
        }

        void RemoveUploadAssets()
        {
            foreach (var assetData in m_SelectedAssetsData.Selection.Cast<UploadAssetData>().Where(x => x.CanBeRemoved).ToList())
            {
                // Similar code to UploadContextMenu.RemoveAssetEntry, find a way to reuse it
                if (ServicesContainer.instance.Resolve<IPageManager>().ActivePage is not UploadPage uploadPage)
                    return;

                uploadPage.RemoveAsset(assetData);
            }
        }

        void IgnoreUploadAssets()
        {
            // Similar code to UploadContextMenu.IgnoreAssetEntry, find a way to reuse it
            foreach (var assetData in m_SelectedAssetsData.Selection.Cast<UploadAssetData>().Where(x => x.CanBeIgnored && !x.IsIgnored).ToList())
            {
                m_PageManager.ActivePage.ToggleAsset(assetData.Identifier, assetData.IsIgnored);
            }
        }

        void IncludeUploadAssets()
        {
            // Similar code to UploadContextMenu.IgnoreAssetEntry, find a way to reuse it
            foreach (var assetData in m_SelectedAssetsData.Selection.Cast<UploadAssetData>().Where(x => x.CanBeIgnored && x.IsIgnored).ToList())
            {
                m_PageManager.ActivePage.ToggleAsset(assetData.Identifier, assetData.IsIgnored);
            }
        }

        void ImportListAsync(List<BaseAssetData> assetsData)
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
                m_AssetImporter.RemoveImports(importedAssets, true);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
        }

        class AssetDataSelection
        {
            public Action<BaseAssetData, AssetDataEventType> AssetDataChanged;

            List<BaseAssetData> m_Selection = new();

            public IReadOnlyCollection<BaseAssetData> Selection
            {
                get => m_Selection;
                set
                {
                    Clear();

                    m_Selection = value.ToList();

                    foreach (var assetData in m_Selection)
                    {
                        assetData.AssetDataChanged += OnAssetDataEvent;
                    }
                }
            }

            void OnAssetDataEvent(BaseAssetData assetData, AssetDataEventType eventType)
            {
                AssetDataChanged?.Invoke(assetData, eventType);
            }

            public bool Exists(Func<BaseAssetData, bool> func)
            {
                return m_Selection.Exists(x => func(x));
            }

            public void Clear()
            {
                foreach (var assetData in m_Selection)
                {
                    assetData.AssetDataChanged -= OnAssetDataEvent;
                }
            }
        }
    }
}
