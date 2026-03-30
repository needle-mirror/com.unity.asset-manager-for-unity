using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Primary metadata filter for restricting results by asset ID (manual entry or deeplink).
    /// Shown in the "Add Filter" dropdown; users can enter one or more asset IDs (comma-separated). Deeplinks apply IDs/versions programmatically.
    /// </summary>
    [Serializable]
    class AssetIdFilter : CloudFilter
    {
        [SerializeField]
        List<string> m_AppliedAssetVersions;

        public AssetIdFilter(IPageFilterStrategy pageFilterStrategy)
            : base(pageFilterStrategy) { }

        public override string DisplayName => Constants.IdText;
        public override FilterSelectionType SelectionType => FilterSelectionType.Text;

        protected override AssetSearchGroupBy GroupBy => AssetSearchGroupBy.Name;

        /// <summary>
        /// Applied asset versions when filter is set (e.g. deeplink). IDs are stored in base SelectedFilters.
        /// </summary>
        public List<string> AppliedAssetVersions => m_AppliedAssetVersions;

        public override bool ApplyFromAssetSearchFilter(AssetSearchFilter searchFilter)
        {
            var hasIds = searchFilter.AssetIds != null && searchFilter.AssetIds.Count > 0;
            var hasVersions = searchFilter.AssetVersions != null && searchFilter.AssetVersions.Count > 0;
            if (!hasIds && !hasVersions)
                return false;

            // Store IDs so the Text UI can show them (comma-separated in a single list element)
            var idsForSelection = hasIds ? new List<string> { string.Join(", ", searchFilter.AssetIds) } : null;
            ApplyFilter(idsForSelection);
            m_AppliedAssetVersions = hasVersions ? new List<string>(searchFilter.AssetVersions) : null;
            return true;
        }

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            assetSearchFilter.AssetIds = ParseAssetIdsFromSelection(SelectedFilters);
            assetSearchFilter.AssetVersions = m_AppliedAssetVersions != null && m_AppliedAssetVersions.Count > 0 ? new List<string>(m_AppliedAssetVersions) : null;
        }

        protected override void ClearFilter()
        {
            var searchFilter = m_PageFilterStrategy.AssetSearchFilter;
            if (searchFilter != null)
            {
                searchFilter.AssetIds = null;
                searchFilter.AssetVersions = null;
            }
            m_AppliedAssetVersions = null;
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            var ids = ParseAssetIdsFromSelection(selectedFilters);
            var searchFilter = m_PageFilterStrategy.AssetSearchFilter;
            searchFilter.AssetIds = ids != null && ids.Count > 0 ? ids : null;
            searchFilter.AssetVersions = null;
        }

        public override string DisplaySelectedFilters()
        {
            var ids = ParseAssetIdsFromSelection(SelectedFilters);
            var idCount = ids != null ? ids.Count : 0;
            var versionCount = m_AppliedAssetVersions != null ? m_AppliedAssetVersions.Count : 0;
            var count = Math.Max(idCount, versionCount);
            if (count == 0)
                return DisplayName;
            if (idCount > 0)
                return idCount == 1 ? $"{DisplayName} : {ids[0]}" : $"{DisplayName} : {ids[0]} +{idCount - 1}";
            return versionCount == 1 ? $"{DisplayName} : (1 version)" : $"{DisplayName} : ({versionCount} versions)";
        }

        public override bool ApplyFilter(List<string> selectedFilters)
        {
            if (selectedFilters == null)
                ClearFilter();
            return base.ApplyFilter(selectedFilters);
        }

        protected override Task<List<FilterSelection>> GetSelectionsAsync()
        {
            return Task.FromResult(new List<FilterSelection>());
        }

        /// <summary>
        /// Parses selected filter value(s) into a list of asset IDs. Supports comma-separated IDs from the Text UI.
        /// </summary>
        static List<string> ParseAssetIdsFromSelection(List<string> selectedFilters)
        {
            if (selectedFilters == null || selectedFilters.Count == 0)
                return null;
            var first = selectedFilters[0];
            if (string.IsNullOrWhiteSpace(first))
                return null;
            if (first.Contains(','))
            {
                var ids = first.Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
                return ids.Count > 0 ? ids : null;
            }
            return new List<string>(selectedFilters);
        }
    }
}
