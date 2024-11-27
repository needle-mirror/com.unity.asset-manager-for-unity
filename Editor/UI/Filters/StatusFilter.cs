using System;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    class StatusFilter : CloudFilter
    {
        public StatusFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider)
            : base(page, projectOrganizationProvider) { }

        public override string DisplayName => "Status";
        protected override AssetSearchGroupBy GroupBy => AssetSearchGroupBy.Status;

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            assetSearchFilter.Status = SelectedFilter;
        }

        protected override void IncludeFilter(string selection)
        {
            m_Page.PageFilters.AssetSearchFilter.Status = selection;
        }

        protected override void ClearFilter()
        {
            m_Page.PageFilters.AssetSearchFilter.Status = null;
        }
    }
}
