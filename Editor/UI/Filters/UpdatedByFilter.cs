using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEditor;

namespace Unity.AssetManager.UI.Editor
{
    class UpdatedByFilter : CloudFilter
    {
        public override string DisplayName => L10n.Tr(Constants.LastEditByText);
        protected override AssetSearchGroupBy GroupBy => AssetSearchGroupBy.UpdatedBy;

        public UpdatedByFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider, IAssetsProvider assetsProvider)
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
            m_Page.PageFilters.AssetSearchFilter.UpdatedBy = null;
        }

        protected override async Task<List<string>> GetSelectionsAsync()
        {
            var selections = await base.GetSelectionsAsync();

            var selectionNames = await GetSelectionNamesAsync(selections);

            return selectionNames;
        }

        async Task<List<string>> GetSelectionNamesAsync(List<string> selections)
        {
            var selectionNames = new List<string>();

            var userName = await m_ProjectOrganizationProvider.SelectedOrganization.GetUserNamesAsync(selections);

            for(var i=0; i<selections.Count; i++)
            {
                var selectionName = selections[i] == "System" ? L10n.Tr("Service Account") : userName[i];
                if (!string.IsNullOrEmpty(selectionName))
                {
                    selectionNames.Add(selectionName);
                }
            }

            selectionNames.Sort();
            return selectionNames;
        }

        async Task<string> GetSelectionId(string selection)
        {
            var userId = await m_ProjectOrganizationProvider.SelectedOrganization.GetUserIdAsync(selection);
            return  userId ?? (selection == L10n.Tr("Service Account") ? "System" : selection);
        }

        async Task ResetSelectedFilterAsync(AssetSearchFilter assetSearchFilter)
        {
            var userIds = new List<string>();
            if (SelectedFilters != null)
            {
                foreach (var selectedFilter in SelectedFilters)
                {
                    userIds.Add(await GetSelectionId(selectedFilter));
                }
            }

            assetSearchFilter.UpdatedBy = userIds;
        }

        async Task IncludeFilterAsync(List<string> selection)
        {
            if(selection == null)
            {
                m_Page.PageFilters.AssetSearchFilter.UpdatedBy = null;
                return;
            }

            var userIds = new List<string>();
            foreach (var selectedFilter in selection)
            {
                userIds.Add(await GetSelectionId(selectedFilter));
            }
            m_Page.PageFilters.AssetSearchFilter.UpdatedBy = userIds;
        }
    }
}
