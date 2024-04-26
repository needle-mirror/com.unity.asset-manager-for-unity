using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    class UnityTypeFilter : CloudFilter
    {
        readonly Dictionary<string, UnityAssetType> m_AssetTypeMap = new();
        List<string> m_Selections = new();

        public override string DisplayName => "Type";
        protected override GroupableField GroupBy => GroupableField.Name;

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
                var regex = AssetDataTypeHelper.GetRegexForExtensions(assetType);
                assetSearchFilter.Include().Files.Path.WithValue(regex);
            }
        }

        protected override void IncludeFilter(string selection)
        {
            if (m_AssetTypeMap.TryGetValue(selection, out var assetType))
            {
                var regex = AssetDataTypeHelper.GetRegexForExtensions(assetType);
                m_Page.PageFilters.AssetFilter.Include().Files.Path.WithValue(regex);
            }
        }

        protected override void ClearFilter()
        {
            m_Page.PageFilters.AssetFilter.Include().Files.Path.Clear();
        }

        protected override Task<List<string>> GetSelectionsAsync()
        {
            ClearFilter();

            return Task.FromResult(m_Selections);
        }

        public void OnAfterDeserialize()
        {
            m_AssetTypeMap.Clear();
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
