using System.Collections.Generic;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    class NumberMetadataFilter : CustomMetadataFilter
    {
        [SerializeReference]
        NumberMetadata m_NumberMetadata;

        public double Value;

        public override FilterSelectionType SelectionType => FilterSelectionType.Number;

        public NumberMetadataFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider, IAssetsProvider assetsProvider, IMetadata metadata)
            : base(page, projectOrganizationProvider, assetsProvider, metadata)
        {
            m_NumberMetadata = metadata as NumberMetadata;
        }

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            assetSearchFilter.CustomMetadata ??= new List<IMetadata>();
            assetSearchFilter.CustomMetadata.Add(m_NumberMetadata);
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            Utilities.DevAssert(selectedFilters == null || selectedFilters.Count == 1, "NumberMetadataFilter: IncludeFilter: selection must be null or have only 1 element");

            m_NumberMetadata.Value = double.Parse(selectedFilters?[0] ?? "0");
            base.IncludeFilter(selectedFilters);
        }

        protected override void ClearFilter()
        {
            base.ClearFilter();
            m_NumberMetadata.Value = 0;
        }

        public override string DisplaySelectedFilters()
        {
            if(SelectedFilters is { Count: 1 })
            {
                return $"{DisplayName} : {m_NumberMetadata.Value}";
            }

            return base.DisplaySelectedFilters();
        }
    }
}
