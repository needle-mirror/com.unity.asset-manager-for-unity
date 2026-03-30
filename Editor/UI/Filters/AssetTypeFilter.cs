using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    [Serializable]
    class AssetTypeFilter : CloudFilter
    {
        public override string DisplayName => "Asset Type";
        public override FilterSelectionType SelectionType => FilterSelectionType.MultiSelection;
        protected override AssetSearchGroupBy GroupBy => AssetSearchGroupBy.Type;

        public AssetTypeFilter(IPageFilterStrategy pageFilterStrategy)
            : base(pageFilterStrategy) { }

        public override bool ApplyFromAssetSearchFilter(AssetSearchFilter searchFilter)
        {
            ClearFilter();

            // Try preferred AssetTypes first
            if (searchFilter.AssetTypes != null && searchFilter.AssetTypes.Count > 0)
            {
                var typeStrings = searchFilter.AssetTypes
                    .Select(t => m_PageFilterStrategy.ToString(t))
                    .ToList();
                ApplyFilter(typeStrings);
                return true;
            }

            // Fall back to legacy AssetTypeStrings
            if (searchFilter.AssetTypeStrings != null && searchFilter.AssetTypeStrings.Count > 0)
            {
                var typeStrings = searchFilter.AssetTypeStrings
                    .Select(legacyString => m_PageFilterStrategy.ConvertAssetTypeFromLegacy(legacyString))
                    .ToList();
                ApplyFilter(typeStrings);
                return true;
            }

            return false;
        }

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            if (SelectedFilters == null || SelectedFilters.Count == 0)
            {
                assetSearchFilter.AssetTypes = null;
                return;
            }

            assetSearchFilter.AssetTypes = SelectedFilters
                .Select(filter => m_PageFilterStrategy.ParseAssetType(filter))
                .ToList();
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            if (selectedFilters == null || selectedFilters.Count == 0)
            {
                m_PageFilterStrategy.AssetSearchFilter.AssetTypes = null;
                return;
            }

            m_PageFilterStrategy.AssetSearchFilter.AssetTypes = selectedFilters
                .Select(filter => m_PageFilterStrategy.ParseAssetType(filter))
                .ToList();
        }

        protected override void ClearFilter()
        {
            m_PageFilterStrategy.AssetSearchFilter.AssetTypes = null;
        }
    }
}
