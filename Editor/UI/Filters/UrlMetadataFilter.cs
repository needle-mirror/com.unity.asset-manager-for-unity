using System.Collections.Generic;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    class UrlMetadataFilter : CustomMetadataFilter
    {
        [SerializeReference]
        UrlMetadata m_UrlMetadata;

        public override FilterSelectionType SelectionType => FilterSelectionType.Url;

        public UrlMetadataFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider, IAssetsProvider assetsProvider, IMetadata metadata)
            : base(page, projectOrganizationProvider, assetsProvider, metadata)
        {
            m_UrlMetadata = metadata as UrlMetadata;
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            m_UrlMetadata.Value = new UriEntry(m_UrlMetadata.Value.Uri, selectedFilters?[0] ?? string.Empty);
            base.IncludeFilter(selectedFilters);
        }
    }
}
