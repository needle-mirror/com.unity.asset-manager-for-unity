using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class InProjectPage : BasePage
    {
        public override bool DisplaySearchBar => false;

        protected override List<BaseFilter> InitFilters()
        {
            return new List<BaseFilter>
            {
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
            Clear(true, clearSelection);
        }

        protected internal override async IAsyncEnumerable<IAssetData> LoadMoreAssets(
            [EnumeratorCancellation] CancellationToken token)
        {
            Utilities.DevLog($"Retrieving import data for {m_AssetDataManager.ImportedAssetInfos.Count} asset(s)...");

            var sortedImportedAssets = SortImportedAssets(m_AssetDataManager.ImportedAssetInfos);

            foreach (var assetData in sortedImportedAssets.Select(a => a.AssetData))
            {
                if (assetData == null) // Can happen with corrupted serialization
                    continue;

                if (await IsDiscardedByLocalFilter(assetData))
                    continue;

                yield return assetData;
            }

            m_CanLoadMoreItems = false;

            await Task.CompletedTask; // Remove warning about async
        }

        protected override void OnLoadMoreSuccessCallBack()
        {
            PageFilters.EnableFilters(m_AssetList.Any());
            SetMessageData(!m_AssetList.Any() ? L10n.Tr(Constants.EmptyInProjectText) : string.Empty,
                RecommendedAction.None);
        }

        IEnumerable<ImportedAssetInfo> SortImportedAssets(IEnumerable<ImportedAssetInfo> importedAssets)
        {
            var sortingOrder = m_PageManager.SortingOrder;

            switch (m_PageManager.SortField)
            {
                case SortField.Name:
                    return sortingOrder == SortingOrder.Ascending
                        ? importedAssets.OrderBy(a => a.AssetData?.Name).ToList()
                        : importedAssets.OrderByDescending(a => a.AssetData?.Name).ToList();
                case SortField.Updated:
                    return sortingOrder == SortingOrder.Ascending
                        ? importedAssets.OrderBy(a => a.AssetData?.Updated).ToList()
                        : importedAssets.OrderByDescending(a => a.AssetData?.Updated).ToList();
                case SortField.Created:
                    return sortingOrder == SortingOrder.Ascending
                        ? importedAssets.OrderBy(a => a.AssetData?.Created).ToList()
                        : importedAssets.OrderByDescending(a => a.AssetData?.Created).ToList();
                case SortField.Description:
                    return sortingOrder == SortingOrder.Ascending
                        ? importedAssets.OrderBy(a => a.AssetData?.Description).ToList()
                        : importedAssets.OrderByDescending(a => a.AssetData?.Description).ToList();
                case SortField.PrimaryType:
                    return sortingOrder == SortingOrder.Ascending
                        ? importedAssets.OrderBy(a => a.AssetData?.AssetType).ToList()
                        : importedAssets.OrderByDescending(a => a.AssetData?.AssetType).ToList();
                case SortField.Status:
                    return sortingOrder == SortingOrder.Ascending
                        ? importedAssets.OrderBy(a => a.AssetData?.Status).ToList()
                        : importedAssets.OrderByDescending(a => a.AssetData?.Status).ToList();
            }

            return importedAssets;
        }
    }
}
