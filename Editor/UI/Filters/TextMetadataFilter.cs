using System.Collections.Generic;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    class TextMetadataFilter : CustomMetadataFilter
    {
        [SerializeReference]
        TextMetadata m_TextMetadata;

        public override FilterSelectionType SelectionType => FilterSelectionType.Text;

        public TextMetadataFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider, IAssetsProvider assetsProvider, IMetadata metadata)
            : base(page, projectOrganizationProvider, assetsProvider, metadata)
        {
            m_TextMetadata = metadata as TextMetadata;
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            m_TextMetadata.Value = selectedFilters?[0];
            base.IncludeFilter(selectedFilters);
        }
    }
}
