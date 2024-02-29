using System.Threading;
using Unity.Cloud.Assets;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    class AssetManagerTypeFilter : CloudFilter
    {
        public AssetManagerTypeFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider)
            : base(page, projectOrganizationProvider) { }

        public override string DisplayName => "Type";
        protected override string Criterion => AssetTypeSearchCriteria.SearchKey;

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            assetSearchFilter.Type.Include(SelectedFilter);
        }

        protected override void IncludeFilter(string selection)
        {
            m_Page.pageFilters.assetFilter.Type.Include(selection);
        }

        protected override void ClearFilter()
        {
            m_Page.pageFilters.assetFilter.Type.Clear();
        }
    }
}
