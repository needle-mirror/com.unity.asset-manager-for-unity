using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    [Serializable]
    class LocalUnityTypeFilter : LocalFilter
    {
        [SerializeReference]
        IAssetDataManager m_AssetDataManager;

        public override string DisplayName => "Type";

        public LocalUnityTypeFilter(IPageFilterStrategy pageFilterStrategy, IAssetDataManager assetDataManager)
            : base(pageFilterStrategy)
        {
            m_AssetDataManager = assetDataManager;
        }

        public override Task<List<FilterSelection>> GetSelections(bool _ = false)
        {
            // Rebuild on every call rather than memoizing: an imported asset's type can change after
            // this filter is created (it starts as AssetType.Other from tracking and is later resolved
            // from the cache/background refresh). Caching the first result would leave the menu stuck
            // on the initial "Other"-only list for the rest of the session.
            var values = m_AssetDataManager.ImportedAssetInfos.Select(i => i.AssetData.AssetType).Distinct();
            var selections = values.Select(x => new FilterSelection(m_PageFilterStrategy.ToString(x), x.GetToolTip())).ToList();

            return Task.FromResult(selections);
        }

        public override Task<bool> Contains(BaseAssetData assetData, CancellationToken token = default)
        {
            return Task.FromResult(SelectedFilters == null || SelectedFilters.Any(selectedFilter => m_PageFilterStrategy.ToString(assetData.AssetType) == selectedFilter));
        }
    }
}
