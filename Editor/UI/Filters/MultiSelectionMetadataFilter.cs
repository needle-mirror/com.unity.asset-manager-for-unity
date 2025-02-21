using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    class MultiSelectionMetadataFilter : CustomMetadataFilter
    {
        [SerializeReference]
        MultiSelectionMetadata m_MultiSelectionMetadata;

        public MultiSelectionMetadataFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider, IAssetsProvider assetsProvider,
            IMetadata metadata) : base(page, projectOrganizationProvider, assetsProvider, metadata)
        {
            m_MultiSelectionMetadata = metadata as MultiSelectionMetadata;
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            m_MultiSelectionMetadata.Value = selectedFilters;
            base.IncludeFilter(selectedFilters);
        }
    }
}
