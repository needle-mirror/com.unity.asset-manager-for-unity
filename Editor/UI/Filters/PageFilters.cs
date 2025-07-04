using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    [Serializable]
    class PageFilters
    {
        [SerializeField]
        List<string> m_SearchFilters = new();

        [SerializeReference]
        List<BaseFilter> m_SelectedFilters = new();

        [SerializeReference]
        List<BaseFilter> m_PrimaryMetadataFilters;

        [SerializeReference]
        List<CustomMetadataFilter> m_CustomMetadataFilters;

        [SerializeReference]
        bool m_IsEnabled;

        [SerializeReference]
        IPage m_Page;

        public List<string> SearchFilters => m_SearchFilters;
        public List<BaseFilter> SelectedFilters => m_SelectedFilters;
        public IEnumerable<LocalFilter> SelectedLocalFilters => m_SelectedFilters.OfType<LocalFilter>();
        public AssetSearchFilter AssetSearchFilter => m_AssetSearchFilter ?? InitializeAssetSearchFilter();
        public List<CustomMetadataFilter> CustomMetadataFilters => m_CustomMetadataFilters;
        IEnumerable<BaseFilter> Filters => m_PrimaryMetadataFilters.Concat(m_CustomMetadataFilters);

        public event Action<IReadOnlyCollection<string>> SearchFiltersChanged;
        public event Action<bool> EnableStatusChanged;
        public event Action<BaseFilter, bool> FilterAdded;
        public event Action<BaseFilter> FilterApplied;

        AssetSearchFilter m_AssetSearchFilter;

        public PageFilters(IPage page)
        {
            m_Page = page;
        }

        public void Initialize(List<BaseFilter> primaryMetadataFilters, List<CustomMetadataFilter> customMetadataFilters)
        {
            m_PrimaryMetadataFilters = primaryMetadataFilters;
            m_CustomMetadataFilters = customMetadataFilters;
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

            foreach (var filterType in SelectedFilters)
            {
                filterType.IsDirty = true;
            }

            SearchFiltersChanged?.Invoke(m_SearchFilters);
        }

        public void RemoveSearchFilter(string searchFilter)
        {
            if (!m_SearchFilters.Contains(searchFilter))
                return;

            m_SearchFilters.Remove(searchFilter);

            foreach (var filterType in SelectedFilters)
            {
                filterType.IsDirty = true;
            }

            SearchFiltersChanged?.Invoke(m_SearchFilters);
        }

        public void ClearSearchFilters()
        {
            if (m_SearchFilters.Count == 0)
                return;

            m_SearchFilters.Clear();

            foreach (var filterType in SelectedFilters)
            {
                filterType.IsDirty = true;
            }

            SearchFiltersChanged?.Invoke(m_SearchFilters);
        }

        public void AddFilter(BaseFilter filter, bool showSelection)
        {
            m_SelectedFilters.Add(filter);
            filter.IsDirty = true;
            FilterAdded?.Invoke(filter, showSelection);
        }

        public void RemoveFilter(BaseFilter filter)
        {
            m_SelectedFilters.Remove(filter);
        }

        public async Task ApplyFilter(Type filterType, List<string> selectedFilters)
        {
            var filter = Filters.FirstOrDefault(f => f.GetType() == filterType);
            if (filter == null)
                return;

            if(selectedFilters != null)
            {
                if(!m_SelectedFilters.Contains(filter))
                {
                    AddFilter(filter, false);
                    await filter.GetSelections();
                }
                else if(filter.SelectedFilters.SequenceEqual(selectedFilters))
                {
                    return;
                }
            }

            ApplyFilter(filter, selectedFilters);
            filter.IsDirty = true;
        }

        public void ApplyFilter(BaseFilter filter, List<string> selectedFilters)
        {
            var reload = filter.ApplyFilter(selectedFilters);
            if (reload)
            {
                foreach (var selectedFilter in m_SelectedFilters)
                {
                    selectedFilter.IsDirty = selectedFilter != filter;
                }
            }

            FilterApplied?.Invoke(filter);

            if (reload)
            {
                m_Page?.Clear(true);
            }
        }

        public void EnableFilters(bool value = true)
        {
            m_IsEnabled = value;
            EnableStatusChanged?.Invoke(IsAvailableFilters());
        }

        public bool IsAvailableFilters()
        {
            return m_IsEnabled && m_SelectedFilters.Count < Filters.Count();
        }

        public List<BaseFilter> GetAvailablePrimaryMetadataFilters()
        {
            return m_PrimaryMetadataFilters.Where(filter => !m_SelectedFilters.Contains(filter)).ToList();
        }

        public List<CustomMetadataFilter> GetAvailableCustomMetadataFilters()
        {
            return m_CustomMetadataFilters.Where(filter => !m_SelectedFilters.Contains(filter)).ToList();
        }

        public void ClearFilters()
        {
            foreach (var filter in SelectedFilters)
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
