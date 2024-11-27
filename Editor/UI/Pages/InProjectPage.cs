using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    static partial class UssStyle
    {
        public const string InProjectPageCustomSection = "in-project-page-custom-section";
        public const string InProjectPageActionSection = "in-project-page-action-section";
        public const string InProjectPageAllActionsSection = "in-project-page-all-actions-section";
        public const string InProjectPageUpdateAllButton = "in-project-page-update-all-button";
    }

    [Serializable]
    class InProjectPage : BasePage
    {
        public override bool DisplaySearchBar => false;

        protected override List<BaseFilter> InitFilters()
        {
            return new List<BaseFilter>
            {
                new LocalImportStatusFilter(this),
                new LocalStatusFilter(this, m_AssetDataManager),
                new LocalUnityTypeFilter(this)
            };
        }

        public InProjectPage(IAssetDataManager assetDataManager, IAssetsProvider assetsProvider,
            IProjectOrganizationProvider projectOrganizationProvider, IPageManager pageManager)
            : base(assetDataManager, assetsProvider, projectOrganizationProvider, pageManager) { }

        public override void OnEnable()
        {
            base.OnEnable();
            m_AssetDataManager.ImportedAssetInfoChanged += OnImportedAssetInfoChanged;

            ResetPreviewStatus();
        }

        public override void OnDisable()
        {
            base.OnDisable();
            m_AssetDataManager.ImportedAssetInfoChanged -= OnImportedAssetInfoChanged;
        }

        void OnImportedAssetInfoChanged(AssetChangeArgs args)
        {
            if (!m_PageManager.IsActivePage(this))
                return;

            var clearSelection = args.Removed.Any(a => a.Equals(LastSelectedAssetId));
            if (clearSelection)
            {
                Clear(true);
            }
        }

        protected internal override async IAsyncEnumerable<BaseAssetData> LoadMoreAssets(
            [EnumeratorCancellation] CancellationToken token)
        {
            Utilities.DevLog($"Retrieving import data for {m_AssetDataManager.ImportedAssetInfos.Count} asset(s)...");

            var tasks = new List<Task<ImportedAssetInfo>>();
            foreach (var assetInfo in m_AssetDataManager.ImportedAssetInfos)
            {
                if (assetInfo.AssetData == null) // Can happen with corrupted serialization
                    continue;

                tasks.Add(IsKeepedByLocalFilterAsync(assetInfo));
            }

            await Task.WhenAll(tasks);

            var filteredImportedAssets = tasks.Select(t => t.Result).Where(a => a != null);
            var sortedImportedAssets = await SortImportedAssetsAsync(filteredImportedAssets);
            foreach (var assetData in sortedImportedAssets.Select(a => a.AssetData))
            {
                yield return assetData;
            }

            m_CanLoadMoreItems = false;
        }

        async Task<ImportedAssetInfo> IsKeepedByLocalFilterAsync(ImportedAssetInfo assetInfo)
        {
            if (await IsDiscardedByLocalFilter(assetInfo.AssetData))
            {
                return null;
            }

            return assetInfo;
        }

        protected override void OnLoadMoreSuccessCallBack()
        {
            PageFilters.EnableFilters(AssetList.Any());
            SetMessageData(!AssetList.Any() ? L10n.Tr(Constants.EmptyInProjectText) : string.Empty,
                RecommendedAction.None);
        }

        protected override VisualElement CreateCustomUISection()
        {
            var root = new VisualElement();
            root.AddToClassList(UssStyle.InProjectPageCustomSection);

            var actions = new VisualElement();
            actions.AddToClassList(UssStyle.InProjectPageAllActionsSection);

            var actionsSection = new VisualElement();
            actionsSection.AddToClassList(UssStyle.InProjectPageActionSection);

            actions.Add(actionsSection);

            var updateAllButton = new Button(UpdateAllToLatest) { text = L10n.Tr(Constants.UpdateAllToLatestActionText) };
            updateAllButton.AddToClassList(UssStyle.InProjectPageUpdateAllButton);
            actionsSection.Add(updateAllButton);

            root.Add(actions);

            return root;
        }

        protected override Dictionary<string,SortField> CreateSortField()
        {
            return new()
            {
                { "Name", SortField.Name },
                { "Last Modified", SortField.Updated },
                { "Upload Date", SortField.Created },
                { "Description", SortField.Description },
                { "Status", SortField.Status },
                { "Import Status", SortField.ImportStatus }
            };
        }

        void ResetPreviewStatus()
        {
            foreach (var assetData in m_AssetDataManager.ImportedAssetInfos.Select(i => i.AssetData))
            {
                assetData.ResetPreviewStatus();
            }
        }

        static void UpdateAllToLatest()
        {
            var assetImporter = ServicesContainer.instance.Resolve<IAssetImporter>();
            TaskUtils.TrackException(assetImporter.UpdateAllToLatestAsync(null, null, CancellationToken.None));

            AnalyticsSender.SendEvent(new UpdateAllLatestButtonClickEvent());
        }

        async Task<IEnumerable<ImportedAssetInfo>> SortImportedAssetsAsync(IEnumerable<ImportedAssetInfo> importedAssets)
        {
            var sortingOrder = m_PageManager.SortingOrder;

            switch (m_PageManager.SortField)
            {
                case SortField.Name:
                    return sortingOrder == SortingOrder.Ascending
                        ? importedAssets.OrderBy(a => a.AssetData?.Name)
                        : importedAssets.OrderByDescending(a => a.AssetData?.Name);
                case SortField.Updated:
                    return sortingOrder == SortingOrder.Ascending
                        ? importedAssets.OrderBy(a => a.AssetData?.Updated).ThenBy(a => a.AssetData?.Name)
                        : importedAssets.OrderByDescending(a => a.AssetData?.Updated).ThenBy(a => a.AssetData?.Name);
                case SortField.Created:
                    return sortingOrder == SortingOrder.Ascending
                        ? importedAssets.OrderBy(a => a.AssetData?.Created).ThenBy(a => a.AssetData?.Name)
                        : importedAssets.OrderByDescending(a => a.AssetData?.Created).ThenBy(a => a.AssetData?.Name);
                case SortField.Description:
                    return sortingOrder == SortingOrder.Ascending
                        ? importedAssets.OrderBy(a => a.AssetData?.Description).ThenBy(a => a.AssetData?.Name)
                        : importedAssets.OrderByDescending(a => a.AssetData?.Description).ThenBy(a => a.AssetData?.Name);
                case SortField.PrimaryType:
                    return sortingOrder == SortingOrder.Ascending
                        ? importedAssets.OrderBy(a => a.AssetData?.AssetType).ThenBy(a => a.AssetData?.Name)
                        : importedAssets.OrderByDescending(a => a.AssetData?.AssetType).ThenBy(a => a.AssetData?.Name);
                case SortField.Status:
                    return sortingOrder == SortingOrder.Ascending
                        ? importedAssets.OrderBy(a => a.AssetData?.Status).ToList()
                        : importedAssets.OrderByDescending(a => a.AssetData?.Status).ToList();
                case SortField.ImportStatus:
                    return await ImportStatusSortAsync(importedAssets);
            }

            return importedAssets;
        }

        async Task<IEnumerable<ImportedAssetInfo>> ImportStatusSortAsync(IEnumerable<ImportedAssetInfo> importedAssets)
        {
            var unityConnectProxy = ServicesContainer.instance.Resolve<IUnityConnectProxy>();

            if (!unityConnectProxy.AreCloudServicesReachable)
                return importedAssets;

            var tasks = await TaskUtils.RunWithMaxConcurrentTasksAsync(importedAssets, CancellationToken.None,
                GetImportStatusAsync, 100);

            return m_PageManager.SortingOrder == SortingOrder.Ascending
                ? tasks
                    .OrderBy(t => ((Task<(ImportedAssetInfo importedAssetInfo, AssetDataStatusType statusType)>)t).Result.statusType).ThenBy(t => ((Task<(ImportedAssetInfo importedAssetInfo, AssetDataStatusType statusType)>)t).Result.importedAssetInfo.AssetData.Name)
                    .Select(t => ((Task<(ImportedAssetInfo importedAssetInfo, AssetDataStatusType statusType)>)t).Result.importedAssetInfo)
                    .ToList()
                : tasks
                    .OrderByDescending(t => ((Task<(ImportedAssetInfo importedAssetInfo, AssetDataStatusType statusType)>)t).Result.statusType).ThenBy(t => ((Task<(ImportedAssetInfo importedAssetInfo, AssetDataStatusType statusType)>)t).Result.importedAssetInfo.AssetData.Name)
                    .Select(t => ((Task<(ImportedAssetInfo importedAssetInfo, AssetDataStatusType statusType)>)t).Result.importedAssetInfo)
                    .ToList();
        }

        async Task<(ImportedAssetInfo importedAssetInfo, AssetDataStatusType statusType)> GetImportStatusAsync(ImportedAssetInfo importedAsset)
        {
            await importedAsset.AssetData.GetPreviewStatusAsync();
            var statusType = importedAsset.AssetData.PreviewStatus.FirstOrDefault();
            return (importedAsset, statusType);
        }
    }
}
