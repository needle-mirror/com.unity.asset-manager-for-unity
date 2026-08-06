using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Upload.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ImportSettings = Unity.AssetManager.Editor.ImportSettings;

namespace Unity.AssetManager.UI.Editor
{
    class MultiAssetDetailsPage : SelectionInspectorPage
    {
        static readonly string k_InspectorScrollviewContainerClassName = "inspector-page-content-container";

        static readonly string k_MultiSelectionFoldoutExpandedClassName = "multi-selection-foldout-expanded";
        static readonly string k_MultiSelectionRemoveName = "multi-selection-remove-button";
        static readonly string k_InspectorFooterContainerName = "footer-container";

        static readonly string k_UnimportedFoldoutName = "multi-selection-unimported-foldout";
        static readonly string k_ImportedFoldoutName = "multi-selection-imported-foldout";
        static readonly string k_UploadRemovedFoldoutName = "multi-selection-upload-removed-foldout";
        static readonly string k_UploadIgnoredFoldoutName = "multi-selection-upload-ignored-foldout";
        static readonly string k_UploadIncludedFoldoutName = "multi-selection-upload-included-foldout";

        static readonly string k_UnimportedFoldoutTitle = "Unimported";
        static readonly string k_ImportedFoldoutTitle = "Imported";
        static readonly string k_UploadRemovedFoldoutTitle = "Included";
        static readonly string k_UploadIgnoredFoldoutTitle = "Ignored Dependencies";
        static readonly string k_UploadIncludedFoldoutTitle = "Included Dependencies";

        readonly AssetDataSelection m_SelectedAssetsData = new();
        readonly IInlineEditService m_InlineEditService;
        InlineMultiEditSection m_InlineMultiEditSection;
        HelpBox m_EditingDisabledHelpBox;
        bool m_IsEditingEnabled = false;

        public enum FoldoutName
        {
            Unimported = 0,
            Imported = 1,
            UploadIgnored = 2,
            UploadIncluded = 3,
            UploadRemoved = 4
        }

        readonly Dictionary<FoldoutName, MultiSelectionFoldout> m_Foldouts = new();

        RemoveButton m_RemoveButton;
        VisualElement m_FooterContainer;
        OperationProgressBar m_OperationProgressBar;

        public MultiAssetDetailsPage(IAssetImporter assetImporter, IAssetOperationManager assetOperationManager,
            IStateManager stateManager, IPageManager pageManager, IAssetDataManager assetDataManager,
            IAssetDatabaseProxy assetDatabaseProxy, IProjectOrganizationProvider projectOrganizationProvider,
            ILinksProxy linksProxy, IUnityConnectProxy unityConnectProxy, IProjectIconDownloader projectIconDownloader,
            IPermissionsManager permissionsManager, IDialogManager dialogManager,
            IInlineEditService inlineEditService = null)
            : base(assetImporter, assetOperationManager, stateManager, pageManager, assetDataManager,
                assetDatabaseProxy, projectOrganizationProvider, linksProxy, unityConnectProxy, projectIconDownloader,
                permissionsManager, dialogManager)
        {
            m_InlineEditService = inlineEditService;
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

            m_Foldouts[FoldoutName.Unimported] = new MultiSelectionFoldout(container, k_UnimportedFoldoutTitle, k_UnimportedFoldoutName,
                Constants.ImportActionText, ImportUnimportedAssetsAsync, k_MultiSelectionFoldoutExpandedClassName);

            m_Foldouts[FoldoutName.Imported] = new MultiSelectionFoldout(container, k_ImportedFoldoutTitle, k_ImportedFoldoutName,
                Constants.ReimportActionText, ReImportAssetsAsync, k_MultiSelectionFoldoutExpandedClassName);

            m_Foldouts[FoldoutName.UploadRemoved] = new MultiSelectionFoldout(container, k_UploadRemovedFoldoutTitle, k_UploadRemovedFoldoutName,
                Constants.RemoveAll, RemoveUploadAssets, k_MultiSelectionFoldoutExpandedClassName);

            m_Foldouts[FoldoutName.UploadIgnored] = new MultiSelectionFoldout(container, k_UploadIgnoredFoldoutTitle, k_UploadIgnoredFoldoutName,
                Constants.IncludeAll, IncludeUploadAssets, k_MultiSelectionFoldoutExpandedClassName);

            m_Foldouts[FoldoutName.UploadIncluded] = new MultiSelectionFoldout(container, k_UploadIncludedFoldoutTitle, k_UploadIncludedFoldoutName,
                Constants.IgnoreAll, IgnoreUploadAssets, k_MultiSelectionFoldoutExpandedClassName);

            foreach (var foldout in m_Foldouts)
            {
                foldout.Value.RegisterValueChangedCallback(value =>
                {
                    m_StateManager.MultiSelectionFoldoutsValues[(int)foldout.Key] = value;
                    RefreshScrollView();
                });
                foldout.Value.Expanded = m_StateManager.MultiSelectionFoldoutsValues[(int)foldout.Key];
            }

            m_FooterContainer = this.Q<VisualElement>(k_InspectorFooterContainerName);
            m_OperationProgressBar = new OperationProgressBar(CancelOrClearImport);
            m_FooterContainer.contentContainer.hierarchy.Add(m_OperationProgressBar);

            m_RemoveButton = new RemoveButton(true)
            {
                text = L10n.Tr(Constants.RemoveAllFromProjectActionText),
                tooltip = L10n.Tr(Constants.RemoveAllFromProjectToolTip),
                name = k_MultiSelectionRemoveName
            };
            m_RemoveButton.RemoveWithExclusiveDependencies += RemoveAllFromLocalProject;
            m_RemoveButton.RemoveOnlySelected += RemoveSelectedFromLocalProject;
            m_RemoveButton.StopTracking += StopTracking;
            m_RemoveButton.StopTrackingOnlySelected += StopTrackingOnlySelected;

            m_FooterContainer.contentContainer.hierarchy.Add(m_RemoveButton);

            if (m_InlineEditService != null)
            {
                var cacheManager = ServicesContainer.instance.Resolve<IAssetDataCacheManager>();
                m_InlineMultiEditSection = new InlineMultiEditSection(
                    m_InlineEditService, m_ProjectOrganizationProvider, m_StateManager,
                    m_AssetDataManager, cacheManager);
                var contentContainer = m_ScrollView.Q<VisualElement>(k_InspectorScrollviewContainerClassName);
                contentContainer.Add(m_InlineMultiEditSection);

                m_EditingDisabledHelpBox = new HelpBox
                {
                    messageType = HelpBoxMessageType.Info
                };
                m_EditingDisabledHelpBox.AddToClassList("editing-disabled-helpbox");
                UIElementsUtils.Hide(m_EditingDisabledHelpBox);
                contentContainer.Add(m_EditingDisabledHelpBox);
            }

            // We need to manually refresh once to make sure the UI is updated when the window is opened.
            if (m_PageManager.ActivePage == null)
                return;

            TaskUtils.TrackException(SelectAssetDataAsync(m_AssetDataManager.GetAssetsData(m_PageManager.ActivePage.SelectedAssets)));
        }

        protected override void RefreshUploadMetadataContainer()
        {
            base.RefreshUploadMetadataContainer();

            if(m_UploadPrimaryMetadataContainer == null || m_PageManager?.ActivePage == null)
                return;

            UIElementsUtils.SetDisplay(m_UploadPrimaryMetadataContainer,
                ((BasePage)m_PageManager.ActivePage).DisplayUploadMetadata && m_IsEditingEnabled);
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

        protected override async Task SelectAssetDataAsync(IReadOnlyCollection<BaseAssetData> assetData)
        {
            if (assetData == null || assetData.Count == 0)
            {
                m_SelectedAssetsData.Clear();
                return;
            }

            // Resolve datasets only for assets that haven't been resolved yet (no known file list)
            var unresolvedAssets = assetData.Where(a => !a.AreDatasetsResolved).ToList();
            if (unresolvedAssets.Any())
            {
                await Task.WhenAll(unresolvedAssets.Select(a => a.ResolveDatasetsAsync()));
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
        }

        public override void ConfigureEditing(EditingMode mode, string disabledReason = null)
        {
            m_IsEditingEnabled = mode == EditingMode.Upload;
            RefreshUploadMetadataContainer();
            m_InlineMultiEditSection?.ConfigureEditing(mode, disabledReason);

            // Show/hide the editing disabled help box
            if (m_EditingDisabledHelpBox != null)
            {
                var showHelpBox = mode == EditingMode.ReadOnly && !string.IsNullOrEmpty(disabledReason);
                if (showHelpBox)
                {
                    m_EditingDisabledHelpBox.text = disabledReason;
                    UIElementsUtils.Show(m_EditingDisabledHelpBox);
                }
                else
                {
                    UIElementsUtils.Hide(m_EditingDisabledHelpBox);
                }
            }
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
            if (m_PageManager.ActivePage is not UploadPage)
            {
                var removable = m_SelectedAssetsData.Selection.Where(x => m_AssetDataManager.IsInProject(x.Identifier)).ToList();
                var isEnabled = removable.Count > 0;
                m_RemoveButton.SetEnabled(isEnabled);
                m_RemoveButton.text = isEnabled ?
                    $"{L10n.Tr(Constants.RemoveAllFromProjectActionText)} ({m_AssetDataManager.FindExclusiveDependencies(removable.Select(x => x.Identifier)).Count})" :
                    L10n.Tr(Constants.RemoveAllFromProjectActionText);
            }

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
            RefreshUploadMetadataContainer();
            RefreshMultiSelectionFoldoutButtonState();
            m_InlineMultiEditSection?.UpdateSelection(m_SelectedAssetsData.Selection);
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
            UIElementsUtils.SetDisplay(m_RemoveButton, true);

            ClearFoldout(FoldoutName.UploadRemoved);
            ClearFoldout(FoldoutName.UploadIgnored);
            ClearFoldout(FoldoutName.UploadIncluded);

            PopulateFoldout(FoldoutName.Unimported, m_SelectedAssetsData.Selection.Where(x => !m_AssetDataManager.IsInProject(x.Identifier)));
            PopulateFoldout(FoldoutName.Imported, m_SelectedAssetsData.Selection.Where(x => m_AssetDataManager.IsInProject(x.Identifier)));
        }

        void RefreshMultiSelectionFoldoutButtonState()
        {
            var cloudServiceReachable = m_UnityConnectProxy?.AreCloudServicesReachable ?? false;

            foreach (var foldout in m_Foldouts)
            {
                foldout.Value.SetButtonEnable(cloudServiceReachable);
            }

            if (m_PageManager.ActivePage is UploadPage)
            {
                // Upload selections use local identifiers; IsInProject only applies to cloud IDs.
                // Unimported/Imported foldouts are cleared on the upload page — keep their actions disabled.
                m_Foldouts[FoldoutName.Unimported].SetButtonEnable(false);
                m_Foldouts[FoldoutName.Imported].SetButtonEnable(false);
                return;
            }

            var containDeletedAssets = m_SelectedAssetsData.Selection.Any(a => a.AssetDataAttributeCollection?.GetAttribute<ImportAttribute>()?.Status == ImportAttribute.ImportStatus.ErrorSync);

            // Disable foldout buttons when all assets in that foldout have no files,
            // and also disable Imported foldout if at least one asset is deleted from the cloud.
            var unimportedAssets = m_SelectedAssetsData.Selection.Where(x => !m_AssetDataManager.IsInProject(x.Identifier)).ToList();
            var importedAssets = m_SelectedAssetsData.Selection.Where(x => m_AssetDataManager.IsInProject(x.Identifier)).ToList();

            // Assets whose datasets are not resolved yet have no file list; they must not count as "no files",
            // otherwise the buttons stay disabled until something else forces the datasets to resolve.
            var unimportedHasAnyWithFiles = unimportedAssets.Any(a => a.MayHaveImportableFiles());
            var importedHasAnyWithFiles = importedAssets.Any(a => a.MayHaveImportableFiles());

            m_Foldouts[FoldoutName.Unimported].SetButtonEnable(cloudServiceReachable && unimportedHasAnyWithFiles);
            m_Foldouts[FoldoutName.Imported].SetButtonEnable(!containDeletedAssets && cloudServiceReachable && importedHasAnyWithFiles);
        }

        void RefreshUploadPageFoldoutUI()
        {
            UIElementsUtils.SetDisplay(m_RemoveButton, false);

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
            m_PageManager.ActivePage.ToggleAsset(m_SelectedAssetsData.Selection.Cast<UploadAssetData>().Where(x => x.CanBeIgnored && !x.IsIgnored).Select(a => a.Identifier).FirstOrDefault(), false);
        }

        void IncludeUploadAssets()
        {
            m_PageManager.ActivePage.ToggleAsset(m_SelectedAssetsData.Selection.Cast<UploadAssetData>().Where(x => x.CanBeIgnored && x.IsIgnored).Select(a => a.Identifier).FirstOrDefault(), true);
        }

        /// <summary>
        /// Resolves the datasets of any asset whose file list is not known yet, before an import
        /// filters on <see cref="BaseAssetDataExtensions.HasImportableFiles"/>.
        /// </summary>
        /// <remarks>
        /// The import buttons are enabled from MayHaveImportableFiles, which counts an unresolved asset
        /// as potentially importable. Without resolving here, a click inside that window would filter
        /// every unresolved asset out, import nothing, and report the assets as having no files when
        /// their files simply had not been fetched yet.
        /// </remarks>
        static void WarnAboutUnreachableFiles(List<BaseAssetData> unreachable)
        {
            if (!unreachable.Any())
                return;

            var names = string.Join(", ", unreachable.Select(a => a.Name));
            Debug.LogWarning($"The files of the following assets could not be retrieved, so they were not imported: {names}");
        }

        /// <returns>
        /// The assets whose file list is still unknown afterwards, because the fetch failed.
        /// ResolveDatasetsAsync swallows an unreachable host, a forbidden asset and a missing asset, so
        /// it returns normally without the files having been retrieved. Those assets must not be
        /// reported as having no files.
        /// </returns>
        static async Task<List<BaseAssetData>> ResolveDatasetsBeforeImportAsync(IEnumerable<BaseAssetData> assetsData)
        {
            var unresolved = assetsData.Where(ad => ad != null && !ad.AreDatasetsResolved).ToList();
            if (unresolved.Any())
            {
                await Task.WhenAll(unresolved.Select(ad => ad.ResolveDatasetsAsync()));
            }

            return unresolved.Where(ad => !ad.AreDatasetsResolved).ToList();
        }

        void ImportListAsync(List<BaseAssetData> assetsData, bool isReimport)
        {
            try
            {
                var source = isReimport ? ImportTrigger.ReimportMultiselect : ImportTrigger.ImportMultiselect;
                m_AssetImporter.StartImportAsync(source, assetsData, new ImportSettings {Type = ImportOperation.ImportType.UpdateToLatest});
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
        }

        void ImportUnimportedAssetsAsync()
        {
            TaskUtils.TrackException(ImportUnimportedAssetsInternalAsync());
        }

        async Task ImportUnimportedAssetsInternalAsync()
        {
            var allUnimported = m_PageManager.ActivePage.SelectedAssets.Where(x => !m_AssetDataManager.IsInProject(x))
                .Select(x => m_AssetDataManager.GetAssetData(x)).ToList();

            var unreachable = await ResolveDatasetsBeforeImportAsync(allUnimported);
            WarnAboutUnreachableFiles(unreachable);

            var candidates = allUnimported.Except(unreachable).ToList();
            var importable = candidates.Where(ad => ad.HasImportableFiles()).ToList();
            var skipped = candidates.Where(ad => !ad.HasImportableFiles()).ToList();

            if (skipped.Any())
            {
                var names = string.Join(", ", skipped.Select(a => a.Name));
                Debug.LogWarning($"The following assets were skipped because they have no files: {names}");
            }

            if (!importable.Any())
                return;

            AnalyticsSender.SendEvent(importable.Count > 1
                ? new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.ImportAll)
                : new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.Import));

            ImportListAsync(importable, false);
        }

        void ReImportAssetsAsync()
        {
            TaskUtils.TrackException(ReImportAssetsInternalAsync());
        }

        async Task ReImportAssetsInternalAsync()
        {
            var allImported = m_PageManager.ActivePage.SelectedAssets.Where(x => m_AssetDataManager.IsInProject(x))
                .Select(x => m_AssetDataManager.GetAssetData(x)).ToList();

            var unreachable = await ResolveDatasetsBeforeImportAsync(allImported);
            WarnAboutUnreachableFiles(unreachable);

            var candidates = allImported.Except(unreachable).ToList();
            var importable = candidates.Where(ad => ad.HasImportableFiles()).ToList();
            var skipped = candidates.Where(ad => !ad.HasImportableFiles()).ToList();

            if (skipped.Any())
            {
                var names = string.Join(", ", skipped.Select(a => a.Name));
                Debug.LogWarning($"The following assets were skipped because they have no files: {names}");
            }

            if (!importable.Any())
                return;

            AnalyticsSender.SendEvent(importable.Count > 1
                ? new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.ReImportAll)
                : new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.Reimport));

            ImportListAsync(importable, true);
        }

        void RemoveAllFromLocalProject()
        {
            var importedAssetIdentifiers = m_PageManager.ActivePage.SelectedAssets.Where(x => m_AssetDataManager.IsInProject(x)).ToList();

            AnalyticsSender.SendEvent(importedAssetIdentifiers.Count > 1
                ? new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.RemoveAll)
                : new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.Remove));

            try
            {
                var removeAssets = m_AssetDataManager.FindExclusiveDependencies(importedAssetIdentifiers);
                m_AssetImporter.RemoveImports(removeAssets.ToList(), true);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
        }

        void RemoveSelectedFromLocalProject()
        {
            var importedAssetIdentifiers = m_PageManager.ActivePage.SelectedAssets.Where(x => m_AssetDataManager.IsInProject(x)).ToList();

            AnalyticsSender.SendEvent(importedAssetIdentifiers.Count > 1
                ? new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.RemoveSelectedAll)
                : new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.RemoveSelected));

            try
            {
                m_AssetImporter.RemoveImports(importedAssetIdentifiers, true);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
        }

        void StopTracking()
        {
            var importedAssetIdentifiers = m_PageManager.ActivePage.SelectedAssets.Where(x => m_AssetDataManager.IsInProject(x)).ToList();

            AnalyticsSender.SendEvent(importedAssetIdentifiers.Count > 1
                ? new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.StopTrackingAll)
                : new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.StopTracking));

            try
            {
                var removeAssets = m_AssetDataManager.FindExclusiveDependencies(importedAssetIdentifiers);
                m_AssetImporter.StopTrackingAssets(removeAssets.ToList());
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
        }

        void StopTrackingOnlySelected()
        {
            var importedAssetIdentifiers = m_PageManager.ActivePage.SelectedAssets.Where(x => m_AssetDataManager.IsInProject(x)).ToList();

            AnalyticsSender.SendEvent(importedAssetIdentifiers.Count > 1
                ? new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.StopTrackingSelectedAll)
                : new DetailsButtonClickedEvent(DetailsButtonClickedEvent.ButtonType.StopTrackingSelected));

            try
            {
                m_AssetImporter.StopTrackingAssets(importedAssetIdentifiers);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
        }
    }
}
