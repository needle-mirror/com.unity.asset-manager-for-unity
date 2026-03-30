using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    [Serializable]
    class NameFilter : CloudFilter
    {
        public NameFilter(IPageFilterStrategy pageFilterStrategy)
            : base(pageFilterStrategy) { }

        public override string DisplayName => "Name";
        public override FilterSelectionType SelectionType => FilterSelectionType.MultiText;
        protected override AssetSearchGroupBy GroupBy => AssetSearchGroupBy.Name;

        public override bool ApplyFromAssetSearchFilter(AssetSearchFilter searchFilter)
        {
            ClearFilter();

            if (!searchFilter.NameFilter.HasFilters)
                return false;

            var filters = new List<string>();

            // First element is the logic mode
            filters.Add(searchFilter.NameFilter.UseAndLogic ? Constants.And : Constants.Or);

            if (searchFilter.NameFilter.Included != null)
            {
                foreach (var name in searchFilter.NameFilter.Included)
                {
                    filters.Add(Constants.Contains);
                    filters.Add(name);
                }
            }

            if (searchFilter.NameFilter.Excluded != null)
            {
                foreach (var name in searchFilter.NameFilter.Excluded)
                {
                    filters.Add(Constants.DoesNotContain);
                    filters.Add(name);
                }
            }

            ApplyFilter(filters);
            return true;
        }

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            ParseSelectedFilters(out var filterData);
            assetSearchFilter.NameFilter = filterData;
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            ParseFilters(selectedFilters, out var filterData);
            m_PageFilterStrategy.AssetSearchFilter.NameFilter = filterData;
        }

        protected override void ClearFilter()
        {
            m_PageFilterStrategy.AssetSearchFilter.NameFilter.Clear();
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
            var nameFilter = m_PageFilterStrategy.AssetSearchFilter.NameFilter;
            var actualFilters = new List<string>();

            if (nameFilter.Included != null)
                actualFilters.AddRange(nameFilter.Included);
            if (nameFilter.Excluded != null)
                actualFilters.AddRange(nameFilter.Excluded);

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
