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
    class LocalStatusFilter : LocalFilter
    {
        [SerializeReference]
        IAssetDataManager m_AssetDataManager;

        public override string DisplayName => "Status";

        public LocalStatusFilter(IPageFilterStrategy pageFilterStrategy, IAssetDataManager assetDataManager)
            : base(pageFilterStrategy)
        {
            m_AssetDataManager = assetDataManager;
        }

        public override Task<List<FilterSelection>> GetSelections(bool _ = false)
        {
            // Rebuild on every call rather than memoizing, so the menu reflects the current set of
            // imported asset statuses even when imported data changes after the filter is created.
            var values = m_AssetDataManager.ImportedAssetInfos.Select(i => i.AssetData.Status).Distinct();
            var selections = values.Select(x => new FilterSelection(x)).ToList();

            return Task.FromResult(selections);
        }

        public override Task<bool> Contains(BaseAssetData assetData, CancellationToken token = default)
        {
            return Task.FromResult(SelectedFilters == null || SelectedFilters.Any(selectedFilter => assetData.Status == selectedFilter));
        }
    }
}
