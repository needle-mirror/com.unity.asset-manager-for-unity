using System;
using System.Collections.Generic;
using System.Linq;
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

        public UnityTypeFilter(IPage page, IProjectOrganizationProvider projectOrganizationProvider, IAssetsProvider assetsProvider)
            : base(page, projectOrganizationProvider, assetsProvider)
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
            var unityTypes = new List<UnityAssetType>();

            foreach (var selectedFilter in m_SelectedFilters)
            {
                if (m_AssetTypeMap.TryGetValue(selectedFilter, out var assetType))
                {
                    unityTypes.Add(assetType);
                }
            }

            assetSearchFilter.AssetTypes = unityTypes.Select(x => x.ConvertUnityAssetTypeToAssetType()).ToList();
        }

        protected override void IncludeFilter(List<string> selectedFilters)
        {
            if(selectedFilters == null)
            {
                m_Page.PageFilters.AssetSearchFilter.AssetTypes = null;
                return;
            }

            var unityTypes = new List<UnityAssetType>();

            foreach (var selectedFilter in selectedFilters)
            {
                if (m_AssetTypeMap.TryGetValue(selectedFilter, out var assetType))
                {
                    unityTypes.Add(assetType);
                }
            }

            m_Page.PageFilters.AssetSearchFilter.AssetTypes = unityTypes.Select(x => x.ConvertUnityAssetTypeToAssetType()).ToList();
        }

        protected override void ClearFilter()
        {
            m_Page.PageFilters.AssetSearchFilter.AssetTypes = null;
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
