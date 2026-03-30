using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    [Serializable]
    class UploadDateFilter : CloudFilter
    {
        [SerializeField]
        DateTime m_DateValue;

        [SerializeField]
        string m_Mode;

        public DateTime DateValue => m_DateValue;
        public string Mode => m_Mode;

        public override string DisplayName => L10n.Tr(Constants.UploadDateText);
        public override FilterSelectionType SelectionType => FilterSelectionType.DateSelection;
        protected override AssetSearchGroupBy GroupBy => AssetSearchGroupBy.Name;

        public UploadDateFilter(IPageFilterStrategy pageFilterStrategy)
            : base(pageFilterStrategy)
        {
            m_DateValue = DateTime.MinValue;
            m_Mode = Constants.Is;
        }

        public override bool ApplyFromAssetSearchFilter(AssetSearchFilter searchFilter)
        {
            ClearFilter();

            if (!searchFilter.CreatedAtIncluded.HasValue && !searchFilter.CreatedAtExcluded.HasValue)
                return false;

            if (searchFilter.CreatedAtIncluded.HasValue)
            {
                ApplyFilter(new List<string> { Constants.Is, searchFilter.CreatedAtIncluded.Value.ToString("o") });
                return true;
            }

            if (searchFilter.CreatedAtExcluded.HasValue)
            {
                ApplyFilter(new List<string> { Constants.IsNot, searchFilter.CreatedAtExcluded.Value.ToString("o") });
                return true;
            }

            return false;
        }

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            assetSearchFilter.CreatedAtIncluded = null;
            assetSearchFilter.CreatedAtExcluded = null;

            if (m_DateValue == DateTime.MinValue)
                return;

            if (m_Mode == Constants.Is)
            {
                assetSearchFilter.CreatedAtIncluded = m_DateValue.Date;
            }
            else
            {
                assetSearchFilter.CreatedAtExcluded = m_DateValue.Date;
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
            m_PageFilterStrategy.AssetSearchFilter.CreatedAtIncluded = null;
            m_PageFilterStrategy.AssetSearchFilter.CreatedAtExcluded = null;
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
