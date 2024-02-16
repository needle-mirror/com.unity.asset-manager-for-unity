using System.Threading;
using Unity.Cloud.Assets;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal class AssetManagerTypeFilter : CloudFilter
    {
        public AssetManagerTypeFilter(IProjectOrganizationProvider projectOrganizationProvider, IAssetsProvider assetsProvider)
            : base(projectOrganizationProvider, assetsProvider) { }

        public override string DisplayName => "Type";
        protected override string Criterion => AssetTypeSearchCriteria.SearchKey;

        protected override void IncludeFilter(string selection)
        {
            m_AssetsProvider.AssetFilter.Type.Include(selection);
        }

        protected override void ClearFilter()
        {
            m_AssetsProvider.AssetFilter.Type.Clear();
        }

        protected override void ResetSelectedFilter()
        {
            m_AssetsProvider.AssetFilter.Type.Include(SelectedFilter);
        }
    }
}
