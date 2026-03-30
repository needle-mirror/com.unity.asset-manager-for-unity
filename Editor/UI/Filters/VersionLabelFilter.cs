using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    [Serializable]
    class VersionLabelFilter : CloudFilter
    {
        public override string DisplayName => "Version Label";
        public override FilterSelectionType SelectionType => FilterSelectionType.MultiSelection;
        protected override AssetSearchGroupBy GroupBy => AssetSearchGroupBy.Label;

        public VersionLabelFilter(IPageFilterStrategy pageFilterStrategy)
            : base(pageFilterStrategy) { }

        public override bool ApplyFromAssetSearchFilter(AssetSearchFilter searchFilter)
        {
            ClearFilter();

            if (searchFilter.Labels == null || searchFilter.Labels.Count == 0)
                return false;

            ApplyFilter(searchFilter.Labels);
            return true;
        }

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            assetSearchFilter.Labels = SelectedFilters?.ToList();
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            m_PageFilterStrategy.AssetSearchFilter.Labels = selectedFilters;
        }

        protected override void ClearFilter()
        {
            m_PageFilterStrategy.AssetSearchFilter.Labels = null;
        }
    }
}
