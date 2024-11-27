using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    [Serializable]
    class UnityTypeFilter : CloudFilter, ISerializationCallbackReceiver
    {
        Dictionary<string, UnityAssetType> m_AssetTypeMap = new();
        List<string> m_Selections = new();

        public override string DisplayName => "Type";
        protected override AssetSearchGroupBy GroupBy
        {
            get
            {
                Utilities.DevAssert(false, "Property not used. Filter selection are static. See \"GetSelectionAsync\"");
                throw new InvalidOperationException();
            }
        }

        public UnityTypeFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider)
            : base(page, projectOrganizationProvider)
        {
            var types = (UnityAssetType[])Enum.GetValues(typeof(UnityAssetType));
            foreach (var type in types)
            {
                var text = type.ToString().PascalCaseToSentence();
                m_AssetTypeMap.Add(text, type);
                m_Selections.Add(text);
            }
        }

        public override void ResetSelectedFilter(AssetSearchFilter assetSearchFilter)
        {
            if (m_AssetTypeMap.TryGetValue(SelectedFilter, out var assetType))
            {
                assetSearchFilter.UnityType = assetType;
            }
        }

        protected override void IncludeFilter(string selection)
        {
            if (m_AssetTypeMap.TryGetValue(selection, out var assetType))
            {
                m_Page.PageFilters.AssetSearchFilter.UnityType = assetType;
            }
        }

        protected override void ClearFilter()
        {
            m_Page.PageFilters.AssetSearchFilter.UnityType = null;
        }

        protected override Task<List<string>> GetSelectionsAsync()
        {
            ClearFilter();

            return Task.FromResult(m_Selections);
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            m_AssetTypeMap = new();
            m_Selections.Clear();

            var types = (UnityAssetType[])Enum.GetValues(typeof(UnityAssetType));
            foreach (var type in types)
            {
                var text = type.ToString().PascalCaseToSentence();
                m_AssetTypeMap.Add(text, type);
                m_Selections.Add(text);
            }
        }
    }
}
