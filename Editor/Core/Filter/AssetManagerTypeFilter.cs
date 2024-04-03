
using Unity.Cloud.Assets;

namespace Unity.AssetManager.Editor
{
    class AssetManagerTypeFilter : CloudFilter
    {
        public AssetManagerTypeFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider)
            : base(page, projectOrganizationProvider) { }

        public override string DisplayName => "Type";
        protected override GroupableField GroupBy => GroupableField.Type;

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            assetSearchFilter.Include().Type.WithValue(SelectedFilter);
        }

        protected override void IncludeFilter(string selection)
        {
            m_Page.pageFilters.assetFilter.Include().Type.WithValue(selection);
        }

        protected override void ClearFilter()
        {
            m_Page.pageFilters.assetFilter.Include().Type.Clear();
        }
    }
}
