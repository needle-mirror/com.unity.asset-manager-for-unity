using System.Threading;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal class StatusFilter : CloudFilter
    {
        public StatusFilter(IProjectOrganizationProvider projectOrganizationProvider, IAssetsProvider assetsProvider)
            : base(projectOrganizationProvider, assetsProvider) { }

        public override string DisplayName => "Status";
        protected override string Criterion => "status";

        protected override void IncludeFilter(string selection)
        {
            m_AssetsProvider.AssetFilter.Status.Include(selection);
        }

        protected override void ClearFilter()
        {
            m_AssetsProvider.AssetFilter.Status.Clear();
        }

        protected override void ResetSelectedFilter()
        {
            m_AssetsProvider.AssetFilter.Status.Include(SelectedFilter);
        }
    }
}
