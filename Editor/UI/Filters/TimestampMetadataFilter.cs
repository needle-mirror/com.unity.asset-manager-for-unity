using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    [Serializable]
    class TimestampMetadataFilter : CustomMetadataFilter
    {
        [SerializeReference]
        TimestampMetadata m_TimestampMetadata;

        [SerializeField]
        DateTime m_FromValue;

        [SerializeField]
        DateTime m_ToValue;

        public DateTime FromValue => m_FromValue;
        public DateTime ToValue => m_ToValue;

        public override FilterSelectionType SelectionType => FilterSelectionType.Timestamp;

        public TimestampMetadataFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider, IAssetsProvider assetsProvider, IMetadata metadata)
            : base(page, projectOrganizationProvider, assetsProvider, metadata)
        {
            m_TimestampMetadata = metadata as TimestampMetadata;
            m_FromValue = DateTime.MinValue;
            m_ToValue = DateTime.MinValue;
        }

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            assetSearchFilter.CustomMetadata ??= new List<IMetadata>();
            assetSearchFilter.CustomMetadata.Add(new TimestampMetadata(m_TimestampMetadata.FieldKey, m_TimestampMetadata.Name,  new DateTimeEntry(m_FromValue)));
            assetSearchFilter.CustomMetadata.Add(new TimestampMetadata(m_TimestampMetadata.FieldKey, m_TimestampMetadata.Name, new DateTimeEntry(m_ToValue)));
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            Utilities.DevAssert(selectedFilters == null || selectedFilters.Count == 2, "TimestampMetadataFilter: IncludeFilter: selection must be null or have 2 elements");

            m_FromValue = DateTime.Parse(selectedFilters?[0] ?? "0", DateTimeFormatInfo.CurrentInfo);
            m_ToValue = DateTime.Parse(selectedFilters?[1] ?? "0", DateTimeFormatInfo.CurrentInfo);
            base.IncludeFilter(selectedFilters);
        }

        protected override void ClearFilter()
        {
            base.ClearFilter();
            m_FromValue = DateTime.MinValue;
            m_ToValue = DateTime.MinValue;
        }

        public override string DisplaySelectedFilters()
        {
            if(SelectedFilters is { Count: 2 })
            {
                return $"{DisplayName} : {L10n.Tr(Constants.FromText)} {m_FromValue} {L10n.Tr(Constants.ToText)} {m_ToValue}";
            }

            return base.DisplaySelectedFilters();
        }
    }
}
