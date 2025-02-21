using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    class CreatedByFilter : CloudFilter
    {
        public override string DisplayName => "Created by";
        protected override AssetSearchGroupBy GroupBy => AssetSearchGroupBy.CreatedBy;

        public CreatedByFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider, IAssetsProvider assetsProvider)
            : base(page, projectOrganizationProvider, assetsProvider) { }

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            TaskUtils.TrackException(ResetSelectedFilterAsync(assetSearchFilter));
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            TaskUtils.TrackException(IncludeFilterAsync(selectedFilters));
        }

        protected override void ClearFilter()
        {
            m_Page.PageFilters.AssetSearchFilter.CreatedBy = null;
        }

        protected override async Task<List<string>> GetSelectionsAsync()
        {
            var selections = await base.GetSelectionsAsync();
            var selectionNames = await m_ProjectOrganizationProvider.SelectedOrganization.GetUserNamesAsync(selections);
            selectionNames.Sort();

            return selectionNames;
        }

        async Task ResetSelectedFilterAsync(AssetSearchFilter assetSearchFilter)
        {
            var userIds = new List<string>();
            if (SelectedFilters != null)
            {
                foreach (var selectedFilter in SelectedFilters)
                {
                    userIds.Add(await m_ProjectOrganizationProvider.SelectedOrganization.GetUserIdAsync(selectedFilter));
                }
            }

            assetSearchFilter.CreatedBy = userIds;
        }

        async Task IncludeFilterAsync(List<string> selectedFilters)
        {
            if(selectedFilters == null)
            {
                m_Page.PageFilters.AssetSearchFilter.CreatedBy = null;
                return;
            }

            var userIds = new List<string>();
            foreach (var selectedFilter in selectedFilters)
            {
                userIds.Add(await m_ProjectOrganizationProvider.SelectedOrganization.GetUserIdAsync(selectedFilter));
            }

            m_Page.PageFilters.AssetSearchFilter.CreatedBy = userIds;
        }
    }
}
