using System;
using System.Collections.Generic;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    class StatusFilter : CloudFilter
    {
        public StatusFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider, IAssetsProvider assetsProvider)
            : base(page, projectOrganizationProvider, assetsProvider) { }

        public override string DisplayName => "Status";
        protected override AssetSearchGroupBy GroupBy => AssetSearchGroupBy.Status;

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            assetSearchFilter.Status = SelectedFilters;
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            m_Page.PageFilters.AssetSearchFilter.Status = selectedFilters;
        }

        protected override void ClearFilter()
        {
            m_Page.PageFilters.AssetSearchFilter.Status = null;
        }
    }
}
