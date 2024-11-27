using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;


namespace Unity.AssetManager.UI.Editor
{
    class CreatedByFilter : CloudFilter
    {
        List<UserInfo> m_UserInfos;

        public override string DisplayName => "Created by";
        protected override AssetSearchGroupBy GroupBy => AssetSearchGroupBy.CreatedBy;

        public CreatedByFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider)
            : base(page, projectOrganizationProvider) { }

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            var userInfo = m_UserInfos?.FirstOrDefault(u => u.Name == SelectedFilter);
            assetSearchFilter.CreatedBy = userInfo?.UserId;
        }

        protected override void IncludeFilter(string selection)
        {
            var userInfo = m_UserInfos?.FirstOrDefault(u => u.Name == selection);
            m_Page.PageFilters.AssetSearchFilter.CreatedBy = userInfo?.UserId;
        }

        protected override void ClearFilter()
        {
            m_UserInfos = null;
            m_Page.PageFilters.AssetSearchFilter.CreatedBy = null;
        }

        protected override async Task<List<string>> GetSelectionsAsync()
        {
            var selections = await base.GetSelectionsAsync();
            var selectionNames = new List<string>();

            if (m_UserInfos != null)
            {
                foreach (var selection in selections)
                {
                    var userInfo = m_UserInfos.FirstOrDefault(u => u.UserId == selection);
                    selectionNames.Add(userInfo?.Name ?? selection);
                }

                return selectionNames;
            }

            await m_ProjectOrganizationProvider.SelectedOrganization.GetUserInfosAsync(userInfos =>
            {
                m_UserInfos = userInfos;
            });

            foreach (var selection in selections)
            {
                var userInfo = m_UserInfos.FirstOrDefault(u => u.UserId == selection);
                selectionNames.Add(userInfo?.Name ?? selection);
            }

            selectionNames.Sort();
            return selectionNames;
        }
    }
}
