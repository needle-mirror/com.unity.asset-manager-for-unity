using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cloud.Assets;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class PageFilters
    {
        public event Action<IEnumerable<string>> onSearchFiltersChanged;

        [SerializeField]
        List<string> m_SearchFilters = new();
        public List<string> searchFilters => m_SearchFilters;

        [SerializeReference]
        List<BaseFilter> m_SelectedFilters = new();

        [SerializeReference]
        List<BaseFilter> m_Filters;

        [SerializeReference]
        IPage m_Page;

        public List<BaseFilter> selectedFilters => m_SelectedFilters;
        public IEnumerable<LocalFilter> selectedLocalFilters => m_SelectedFilters.OfType<LocalFilter>();

        AssetSearchFilter m_AssetSearchFilter;

        public AssetSearchFilter assetFilter => m_AssetSearchFilter ?? InitializeAssetSearchFilter();

        public PageFilters(IPage page, List<BaseFilter> filters)
        {
            m_Page = page;
            m_Filters = filters;
        }

        public void AddSearchFilter(IEnumerable<string> searchFiltersArg)
        {
            var searchFilterAdded = false;
            foreach (var searchFilter in searchFiltersArg)
            {
                if (m_SearchFilters.Contains(searchFilter))
                    continue;

                m_SearchFilters.Add(searchFilter);
                searchFilterAdded = true;
            }

            if (!searchFilterAdded)
                return;

            foreach (var filterType in selectedFilters)
            {
                filterType.IsDirty = true;
            }

            onSearchFiltersChanged?.Invoke(m_SearchFilters);
        }

        public void RemoveSearchFilter(string searchFilter)
        {
            if (!m_SearchFilters.Contains(searchFilter))
                return;

            m_SearchFilters.Remove(searchFilter);

            foreach (var filterType in selectedFilters)
            {
                filterType.IsDirty = true;
            }

            onSearchFiltersChanged?.Invoke(m_SearchFilters);
        }

        public void ClearSearchFilters()
        {
            if (m_SearchFilters.Count == 0)
                return;

            m_SearchFilters.Clear();

            foreach (var filterType in selectedFilters)
            {
                filterType.IsDirty = true;
            }

            onSearchFiltersChanged?.Invoke(m_SearchFilters);
        }

        public void AddFilter(BaseFilter filter)
        {
            m_SelectedFilters.Add(filter);
            filter.IsDirty = true;
        }

        public void RemoveFilter(BaseFilter filter)
        {
            m_SelectedFilters.Remove(filter);
        }

        public void ApplyFilter(BaseFilter filter, string selection)
        {
            var reload = filter.ApplyFilter(selection);
            if (reload)
            {
                foreach (var selectedFilter in m_SelectedFilters)
                {
                    selectedFilter.IsDirty = selectedFilter != filter;
                }
            }

            m_Page?.Clear(reload);
        }

        public bool IsAvailableFilters()
        {
            return m_SelectedFilters.Count < m_Filters.Count;
        }

        public List<BaseFilter> GetAvailableFilters()
        {
            return m_Filters.Where(filter => !m_SelectedFilters.Contains(filter)).ToList();
        }

        public void ClearFilters()
        {
            foreach (var filter in selectedFilters)
            {
                filter.ApplyFilter(null);
                filter.Clear();
                filter.IsDirty = true;
            }

            m_SearchFilters.Clear();

            m_Page?.Clear(true);
        }

        AssetSearchFilter InitializeAssetSearchFilter()
        {
            m_AssetSearchFilter = new AssetSearchFilter();

            if (m_SelectedFilters != null && m_SelectedFilters.Any())
            {
                var cloudFilter = m_SelectedFilters.OfType<CloudFilter>();
                foreach (var filter in cloudFilter)
                {
                    filter.ResetSelectedFilter(m_AssetSearchFilter);
                }
            }

            return m_AssetSearchFilter;
        }
    }
}
