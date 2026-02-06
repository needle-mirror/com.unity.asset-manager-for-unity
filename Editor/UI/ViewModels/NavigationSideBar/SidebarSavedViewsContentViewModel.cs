using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    class SidebarSavedViewsFoldoutViewModel
    {

        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IPageManager m_PageManager;
        readonly ISavedAssetSearchFilterManager m_SavedSearchFilterManager;

        public event Action<SavedAssetSearchFilter, bool> FilterSelected;
        public event Action<SavedAssetSearchFilter> FilterAdded;
        public event Action<SavedAssetSearchFilter> FilterDeleted;

        public SidebarSavedViewsFoldoutViewModel(IProjectOrganizationProvider projectOrganizationProvider, IPageManager pageManager,
            ISavedAssetSearchFilterManager savedSearchFilterManager)
        {
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_PageManager = pageManager;
            m_SavedSearchFilterManager = savedSearchFilterManager;
        }

        public void BindEvents()
        {
            m_SavedSearchFilterManager.FilterSelected += OnFilterSelected;
            m_SavedSearchFilterManager.FilterAdded += OnFilterAdded;
            m_SavedSearchFilterManager.FilterDeleted += OnFilterDeleted;
        }

        public void UnbindEvents()
        {
            m_SavedSearchFilterManager.FilterSelected -= OnFilterSelected;
            m_SavedSearchFilterManager.FilterAdded -= OnFilterAdded;
            m_SavedSearchFilterManager.FilterDeleted -= OnFilterDeleted;
        }

        void OnFilterSelected(SavedAssetSearchFilter savedAssetSearchFilter, bool isSelected)
        {
            FilterSelected?.Invoke(savedAssetSearchFilter, isSelected);
        }

        void OnFilterAdded(SavedAssetSearchFilter savedAssetSearchFilter)
        {
            FilterAdded?.Invoke(savedAssetSearchFilter);
        }

        void OnFilterDeleted(SavedAssetSearchFilter savedAssetSearchFilter)
        {
            FilterDeleted?.Invoke(savedAssetSearchFilter);
        }

        public void OnSaveCurrentFilterClicked()
        {
            var sortingOptions = new SortingOptions(m_PageManager.SortField, m_PageManager.SortingOrder);
            m_SavedSearchFilterManager.SaveNewFilter("New saved view", m_PageManager.PageFilterStrategy.AssetSearchFilter, sortingOptions);
        }

        public void ClearSelectedFilter()
        {
            m_SavedSearchFilterManager.ClearSelectedFilter();
        }

        public void SelectFilter(string filterId)
        {
            m_SavedSearchFilterManager.SelectFilter(filterId);
        }

        public SavedAssetSearchFilter GetSelectedFilter()
        {
            return m_SavedSearchFilterManager.SelectedFilter;
        }

        public List<SavedAssetSearchFilter> GetSavedFilters()
        {
            return m_SavedSearchFilterManager.Filters.Where(f => f.OrganizationId == GetSelectedOrganizationId()).ToList();
        }

        public bool IsFilterRenameValid(string targetFilterId, string newName)
        {
            return !m_SavedSearchFilterManager.Filters.Any(f =>
                f.OrganizationId == GetSelectedOrganizationId() && f.FilterName == newName &&
                f.FilterId != targetFilterId);
        }

        public void RenameFilter(SavedAssetSearchFilter savedAssetSearchFilter, string newName)
        {
            if (!IsFilterRenameValid(savedAssetSearchFilter.FilterId, newName)) return;

            savedAssetSearchFilter.SetFilterName(newName);
            m_SavedSearchFilterManager.RenameFilter(savedAssetSearchFilter, newName, false);
        }

        public void DeleteFilter(SavedAssetSearchFilter savedAssetSearchFilter)
        {
            m_SavedSearchFilterManager.DeleteFilter(savedAssetSearchFilter);
        }

        string GetSelectedOrganizationId()
        {
            return m_ProjectOrganizationProvider.SelectedOrganization?.Id;
        }
    }
}
