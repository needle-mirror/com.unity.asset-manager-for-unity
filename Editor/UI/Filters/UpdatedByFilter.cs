using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEditor;

namespace Unity.AssetManager.UI.Editor
{
    class UpdatedByFilter : CloudFilter
    {
        public override string DisplayName => L10n.Tr(Constants.LastEditByText);
        protected override AssetSearchGroupBy GroupBy => AssetSearchGroupBy.UpdatedBy;

        List<UserInfo> m_UserInfos;

        public UpdatedByFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider)
            : base(page, projectOrganizationProvider) { }

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            var userInfo = m_UserInfos?.FirstOrDefault(u => u.Name == SelectedFilter);

            assetSearchFilter.UpdatedBy = userInfo?.UserId ??
                (SelectedFilter == L10n.Tr("Service Account") ? "System" : SelectedFilter);
        }

        protected override void IncludeFilter(string selection)
        {
            var userInfo = m_UserInfos?.FirstOrDefault(u => u.Name == selection);
            m_Page.PageFilters.AssetSearchFilter.UpdatedBy = userInfo?.UserId ??
                (selection == L10n.Tr("Service Account") ? "System" : selection);
        }

        protected override void ClearFilter()
        {
            m_UserInfos = null;
            m_Page.PageFilters.AssetSearchFilter.UpdatedBy = null;
        }

        protected override async Task<List<string>> GetSelectionsAsync()
        {
            var selections = await base.GetSelectionsAsync();

            if (m_UserInfos != null)
            {
                return GetSelectionNames(selections);
            }

            await m_ProjectOrganizationProvider.SelectedOrganization.GetUserInfosAsync(userInfos =>
            {
                m_UserInfos = userInfos;
            });

            while (m_UserInfos == null)
            {
                await Task.Delay(100);
            }

            return GetSelectionNames(selections);
        }

        List<string> GetSelectionNames(List<string> selections)
        {
            var selectionNames = new List<string>();

            if (m_UserInfos != null)
            {
                foreach (var selection in selections)
                {
                    var userInfo = m_UserInfos.FirstOrDefault(u => u.UserId == selection);
                    var selectionName =
                        userInfo?.Name ?? (selection == "System" ? L10n.Tr("Service Account") : selection);
                    if (!string.IsNullOrEmpty(selectionName))
                    {
                        selectionNames.Add(selectionName);
                    }
                }
            }

            selectionNames.Sort();
            return selectionNames;
        }
    }
}
