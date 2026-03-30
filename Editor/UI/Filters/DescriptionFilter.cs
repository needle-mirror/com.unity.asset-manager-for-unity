using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    [Serializable]
    class DescriptionFilter : CloudFilter
    {
        public DescriptionFilter(IPageFilterStrategy pageFilterStrategy)
            : base(pageFilterStrategy) { }

        public override string DisplayName => "Description";
        public override FilterSelectionType SelectionType => FilterSelectionType.MultiText;
        protected override AssetSearchGroupBy GroupBy => AssetSearchGroupBy.Name;

        public override bool ApplyFromAssetSearchFilter(AssetSearchFilter searchFilter)
        {
            ClearFilter();

            if (!searchFilter.DescriptionFilter.HasFilters)
                return false;

            var filters = new List<string>();

            // First element is the logic mode
            filters.Add(searchFilter.DescriptionFilter.UseAndLogic ? Constants.And : Constants.Or);

            if (searchFilter.DescriptionFilter.Included != null)
            {
                foreach (var description in searchFilter.DescriptionFilter.Included)
                {
                    filters.Add(Constants.Contains);
                    filters.Add(description);
                }
            }

            if (searchFilter.DescriptionFilter.Excluded != null)
            {
                foreach (var description in searchFilter.DescriptionFilter.Excluded)
                {
                    filters.Add(Constants.DoesNotContain);
                    filters.Add(description);
                }
            }

            ApplyFilter(filters);
            return true;
        }

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            ParseSelectedFilters(out var filterData);
            assetSearchFilter.DescriptionFilter = filterData;
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            ParseFilters(selectedFilters, out var filterData);
            m_PageFilterStrategy.AssetSearchFilter.DescriptionFilter = filterData;
        }

        protected override void ClearFilter()
        {
            m_PageFilterStrategy.AssetSearchFilter.DescriptionFilter.Clear();
        }

        public override bool ApplyFilter(List<string> selectedFilters)
        {
            base.ApplyFilter(selectedFilters);
            // Always reload when applying this filter
            return true;
        }

        protected override Task<List<FilterSelection>> GetSelectionsAsync()
        {
            // Text filters don't need to fetch selections from the server
            return Task.FromResult(new List<FilterSelection>());
        }

        public override string DisplaySelectedFilters()
        {
            var descriptionFilter = m_PageFilterStrategy.AssetSearchFilter.DescriptionFilter;
            var actualFilters = new List<string>();

            if (descriptionFilter.Included != null)
                actualFilters.AddRange(descriptionFilter.Included);
            if (descriptionFilter.Excluded != null)
                actualFilters.AddRange(descriptionFilter.Excluded);

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

            if (filters == null || filters.Count < 1)
                return;

            // First element is the logic mode (AND/OR)
            var logicMode = filters[0];
            filterData.UseAndLogic = logicMode != Constants.Or;

            // Remaining elements are pairs of (mode, value)
            for (var i = 1; i < filters.Count - 1; i += 2)
            {
                var mode = filters[i];
                var value = filters[i + 1];

                if (string.IsNullOrEmpty(value))
                    continue;

                if (mode == Constants.DoesNotContain)
                {
                    filterData.Excluded.Add(value);
                }
                else
                {
                    filterData.Included.Add(value);
                }
            }
        }
    }
}
