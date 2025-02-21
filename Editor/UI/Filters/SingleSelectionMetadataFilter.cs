using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    class SingleSelectionMetadataFilter : CustomMetadataFilter
    {
        [SerializeReference]
        SingleSelectionMetadata m_SingleSelectionMetadata;

        public override FilterSelectionType SelectionType => FilterSelectionType.SingleSelection;

        public SingleSelectionMetadataFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider, IAssetsProvider assetsProvider, IMetadata metadata)
            : base(page, projectOrganizationProvider, assetsProvider, metadata)
        {
            m_SingleSelectionMetadata = metadata as SingleSelectionMetadata;
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            m_SingleSelectionMetadata.Value = selectedFilters?[0];
            base.IncludeFilter(selectedFilters);
        }
    }
}
