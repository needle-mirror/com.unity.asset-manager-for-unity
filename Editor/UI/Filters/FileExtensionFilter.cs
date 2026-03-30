using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    [Serializable]
    class FileExtensionFilter : CloudFilter
    {
        public override string DisplayName => "File Extension";
        public override FilterSelectionType SelectionType => FilterSelectionType.MultiSelection;
        public override bool AllowCustomChoices => true;
        protected override AssetSearchGroupBy GroupBy => AssetSearchGroupBy.Extension;

        public FileExtensionFilter(IPageFilterStrategy pageFilterStrategy)
            : base(pageFilterStrategy) { }

        public override bool ApplyFromAssetSearchFilter(AssetSearchFilter searchFilter)
        {
            ClearFilter();

            if (searchFilter.Extensions == null || searchFilter.Extensions.Count == 0)
                return false;

            ApplyFilter(searchFilter.Extensions);
            return true;
        }

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            assetSearchFilter.Extensions = SelectedFilters?.ToList();
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            m_PageFilterStrategy.AssetSearchFilter.Extensions = selectedFilters;
        }

        protected override void ClearFilter()
        {
            m_PageFilterStrategy.AssetSearchFilter.Extensions = null;
        }

        protected override Task<List<FilterSelection>> GetSelectionsAsync()
        {
            return m_PageFilterStrategy.GetFilterSelectionsAsync(GroupBy);
        }
    }
}
