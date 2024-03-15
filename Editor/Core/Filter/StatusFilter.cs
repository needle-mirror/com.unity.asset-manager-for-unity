using System.Threading;
using Unity.Cloud.Assets;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    class StatusFilter : CloudFilter
    {
        public StatusFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider)
            : base(page, projectOrganizationProvider) { }

        public override string DisplayName => "Status";
        protected override GroupableField GroupBy => GroupableField.Status;

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            assetSearchFilter.Include().Status.WithValue(SelectedFilter);
        }

        protected override void IncludeFilter(string selection)
        {
            m_Page.pageFilters.assetFilter.Include().Status.WithValue(selection);
        }

        protected override void ClearFilter()  
        {
            m_Page.pageFilters.assetFilter.Include().Status.Clear();
        }
    }
}
