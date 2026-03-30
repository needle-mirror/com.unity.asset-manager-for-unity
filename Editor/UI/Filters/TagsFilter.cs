using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    [Serializable]
    class TagsFilter : CloudFilter
    {
        public override string DisplayName => "Tags";
        public override FilterSelectionType SelectionType => FilterSelectionType.AdvancedMultiSelection;
        protected override AssetSearchGroupBy GroupBy => AssetSearchGroupBy.Tag;

        public TagsFilter(IPageFilterStrategy pageFilterStrategy)
            : base(pageFilterStrategy) { }

        public override bool ApplyFromAssetSearchFilter(AssetSearchFilter searchFilter)
        {
            ClearFilter();

            if (!searchFilter.TagsFilter.HasFilters)
                return false;

            // Format: [included1, included2, ..., "", excluded1, excluded2, ...]
            // Empty string separates included from excluded values
            var included = searchFilter.TagsFilter.Included ?? new List<string>();
            var excluded = searchFilter.TagsFilter.Excluded ?? new List<string>();

            var filters = new List<string>();
            filters.AddRange(included);
            filters.Add(string.Empty); // Separator
            filters.AddRange(excluded);

            ApplyFilter(filters);
            return true;
        }

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            ParseSelectedFilters(out var filterData);
            assetSearchFilter.TagsFilter = filterData;
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            ParseFilters(selectedFilters, out var filterData);
            m_PageFilterStrategy.AssetSearchFilter.TagsFilter = filterData;
        }

        protected override void ClearFilter()
        {
            m_PageFilterStrategy.AssetSearchFilter.TagsFilter.Clear();
        }

        public override bool ApplyFilter(List<string> selectedFilters)
        {
            base.ApplyFilter(selectedFilters);
            // Always reload when applying this filter
            return true;
        }

        public override string DisplaySelectedFilters()
        {
            var tagsFilter = m_PageFilterStrategy.AssetSearchFilter.TagsFilter;
            var actualFilters = new List<string>();

            if (tagsFilter.Included != null)
                actualFilters.AddRange(tagsFilter.Included);
            if (tagsFilter.Excluded != null)
                actualFilters.AddRange(tagsFilter.Excluded);

            if (!actualFilters.Any())
                return DisplayName;

            if (actualFilters.Count == 1)
                return $"{DisplayName} : {actualFilters[0]}";

            return $"{DisplayName} : {actualFilters[0]} +{actualFilters.Count - 1}";
        }

        void ParseSelectedFilters(out MultiTextFilterData filterData)
        {
            ParseFilters(SelectedFilters, out filterData);
        }

        static void ParseFilters(List<string> filters, out MultiTextFilterData filterData)
        {
            filterData = new MultiTextFilterData
            {
                Included = new List<string>(),
                Excluded = new List<string>(),
                UseAndLogic = true // Default to AND
            };

            if (filters == null || filters.Count == 0)
                return;

            // Format: [included1, included2, ..., "", excluded1, excluded2, ...]
            // Empty string separates included from excluded values
            var isExcluded = false;
            foreach (var value in filters)
            {
                if (string.IsNullOrEmpty(value))
                {
                    isExcluded = true;
                    continue;
                }

                if (isExcluded)
                    filterData.Excluded.Add(value);
                else
                    filterData.Included.Add(value);
            }
        }
    }
}
