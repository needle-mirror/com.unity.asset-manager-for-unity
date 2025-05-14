using System.Collections.Generic;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    class NumberRangeMetadataFilter : CustomMetadataFilter
    {
        [SerializeReference]
        Core.Editor.NumberMetadata m_NumberMetadata;

        [SerializeField]
        double m_FromValue;

        [SerializeField]
        double m_ToValue;

        public double FromValue => m_FromValue;
        public double ToValue => m_ToValue;

        public override FilterSelectionType SelectionType => FilterSelectionType.NumberRange;

        public NumberRangeMetadataFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider, IAssetsProvider assetsProvider, IMetadata metadata)
            : base(page, projectOrganizationProvider, assetsProvider, metadata)
        {
            m_NumberMetadata = metadata as Core.Editor.NumberMetadata;
        }

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            assetSearchFilter.CustomMetadata ??= new List<IMetadata>();
            assetSearchFilter.CustomMetadata.Add(new Core.Editor.NumberMetadata(m_NumberMetadata.FieldKey, m_NumberMetadata.Name, m_FromValue));
            assetSearchFilter.CustomMetadata.Add(new Core.Editor.NumberMetadata(m_NumberMetadata.FieldKey, m_NumberMetadata.Name, m_ToValue));
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            Utilities.DevAssert(selectedFilters == null || selectedFilters.Count == 2, "NumberRangeMetadataFilter: IncludeFilter: selection must be null or have 2 elements");

            m_FromValue = double.Parse(selectedFilters?[0] ?? "0");
            m_ToValue = double.Parse(selectedFilters?[1] ?? "0");
            base.IncludeFilter(selectedFilters);
        }

        protected override void ClearFilter()
        {
            base.ClearFilter();
            m_FromValue = 0;
            m_ToValue = 0;
        }

        public override string DisplaySelectedFilters()
        {
            if(SelectedFilters != null && SelectedFilters.Count == 2)
            {
                return $"{DisplayName} : {L10n.Tr(Constants.FromText)} {m_FromValue} {L10n.Tr(Constants.ToText)} {m_ToValue}";
            }

            return base.DisplaySelectedFilters();
        }
    }
}
