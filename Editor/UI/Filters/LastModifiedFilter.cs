using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    [Serializable]
    class LastModifiedFilter : CloudFilter
    {
        [SerializeField]
        DateTime m_DateValue;

        [SerializeField]
        string m_Mode;

        public DateTime DateValue => m_DateValue;
        public string Mode => m_Mode;

        public override string DisplayName => L10n.Tr(Constants.LastModifiedText);
        public override FilterSelectionType SelectionType => FilterSelectionType.DateSelection;
        protected override AssetSearchGroupBy GroupBy => AssetSearchGroupBy.Name;

        public LastModifiedFilter(IPageFilterStrategy pageFilterStrategy)
            : base(pageFilterStrategy)
        {
            m_DateValue = DateTime.MinValue;
            m_Mode = Constants.Is;
        }

        public override bool ApplyFromAssetSearchFilter(AssetSearchFilter searchFilter)
        {
            ClearFilter();

            if (!searchFilter.UpdatedAtIncluded.HasValue && !searchFilter.UpdatedAtExcluded.HasValue)
                return false;

            if (searchFilter.UpdatedAtIncluded.HasValue)
            {
                ApplyFilter(new List<string> { Constants.Is, searchFilter.UpdatedAtIncluded.Value.ToString("o") });
                return true;
            }

            if (searchFilter.UpdatedAtExcluded.HasValue)
            {
                ApplyFilter(new List<string> { Constants.IsNot, searchFilter.UpdatedAtExcluded.Value.ToString("o") });
                return true;
            }

            return false;
        }

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            assetSearchFilter.UpdatedAtIncluded = null;
            assetSearchFilter.UpdatedAtExcluded = null;

            if (m_DateValue == DateTime.MinValue)
                return;

            if (m_Mode == Constants.Is)
            {
                assetSearchFilter.UpdatedAtIncluded = m_DateValue.Date;
            }
            else
            {
                assetSearchFilter.UpdatedAtExcluded = m_DateValue.Date;
            }
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            m_Mode = Constants.Is;
            m_DateValue = DateTime.MinValue;

            if (selectedFilters == null || selectedFilters.Count < 2)
            {
                ResetSelectedFilter(m_PageFilterStrategy.AssetSearchFilter);
                return;
            }

            m_Mode = selectedFilters[0];
            try
            {
                m_DateValue = DateTime.Parse(selectedFilters[1], DateTimeFormatInfo.CurrentInfo, DateTimeStyles.RoundtripKind);
            }
            catch
            {
                m_DateValue = DateTime.MinValue;
            }

            ResetSelectedFilter(m_PageFilterStrategy.AssetSearchFilter);
        }

        protected override void ClearFilter()
        {
            m_PageFilterStrategy.AssetSearchFilter.UpdatedAtIncluded = null;
            m_PageFilterStrategy.AssetSearchFilter.UpdatedAtExcluded = null;
        }

        public override string DisplaySelectedFilters()
        {
            if (SelectedFilters == null || SelectedFilters.Count < 2)
            {
                return base.DisplaySelectedFilters();
            }

            try
            {
                var date = DateTime.Parse(SelectedFilters[1], DateTimeFormatInfo.CurrentInfo, DateTimeStyles.RoundtripKind);
                return $"{DisplayName} : {L10n.Tr(SelectedFilters[0])} {date.ToString(AssetManagerCoreConstants.DateSelectionFormat, CultureInfo.InvariantCulture)}";
            }
            catch
            {
                return $"{DisplayName} : {L10n.Tr(SelectedFilters[0])} {SelectedFilters[1]}";
            }
        }
    }
}
