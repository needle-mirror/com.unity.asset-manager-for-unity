using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    [Serializable]
    abstract class CustomMetadataFilter : CloudFilter
    {
        [SerializeReference]
        IMetadata m_Metadata;

        public override string DisplayName => m_Metadata.Name;

        protected CustomMetadataFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider,
            IAssetsProvider assetsProvider, IMetadata metadata) : base(page, projectOrganizationProvider, assetsProvider)
        {
            m_Metadata = metadata;
        }
        protected override AssetSearchGroupBy GroupBy => AssetSearchGroupBy.Name;
        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            assetSearchFilter.CustomMetadata ??= new List<IMetadata>();
            assetSearchFilter.CustomMetadata.Add(m_Metadata);
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            ResetSelectedFilter(m_Page.PageFilters.AssetSearchFilter);
        }

        protected override void ClearFilter()
        {
            m_Page.PageFilters.AssetSearchFilter.CustomMetadata?.RemoveAll(m => m.FieldKey == m_Metadata.FieldKey);
        }

        protected override async Task<List<string>> GetFilterSelectionsAsync(string organizationId, IEnumerable<string> projectIds, CancellationToken token)
        {
            return await m_AssetsProvider.GetFilterSelectionsAsync(organizationId, projectIds,
                m_Page.PageFilters.AssetSearchFilter, m_Metadata.FieldKey, token);
        }
    }
}
