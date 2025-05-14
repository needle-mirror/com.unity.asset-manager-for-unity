using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    class BooleanMetadataFilter : CustomMetadataFilter
    {
        [SerializeReference]
        Core.Editor.BooleanMetadata m_BooleanMetadata;

        public override FilterSelectionType SelectionType => FilterSelectionType.SingleSelection;

        public BooleanMetadataFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider, IAssetsProvider assetsProvider, IMetadata metadata)
            : base(page, projectOrganizationProvider, assetsProvider, metadata)
        {
            m_BooleanMetadata = metadata as Core.Editor.BooleanMetadata;
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            Utilities.DevAssert(selectedFilters == null || selectedFilters.Count == 1, "BooleanMetadataFilter: IncludeFilter: selection must be null or have 1 element");

            m_BooleanMetadata.Value = bool.Parse(selectedFilters?[0] ?? "false");
            base.IncludeFilter(selectedFilters);
        }

        protected override Task<List<string>> GetSelectionsAsync()
        {
            return Task.FromResult(new List<string> { "True", "False" });
        }
    }
}
