using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class UnityTypeFilter : LocalFilter
    {
        public override string DisplayName => "Type";
        public List<string> m_Selections = new();
        Dictionary<string, UnityAssetType> m_AssetTypeMap = new();

        public UnityTypeFilter(IPageManager pageManager)
            : base(pageManager)
        {
            var types = (UnityAssetType[])Enum.GetValues(typeof(UnityAssetType));
            foreach (var type in types)
            {
                var text = type.ToString().PascalCaseToSentence();
                m_AssetTypeMap.Add(text, type);
                m_Selections.Add(text);
            }
        }

        public override Task<List<string>> GetSelections()
        {
            return Task.FromResult(m_Selections);
        }
        
        public override async Task<bool> Contains(IAssetData assetData)
        {
            if (m_AssetTypeMap == null)
                return true;
            
            var extension = await assetData.GetPrimaryExtension();
            var type = AssetDataTypeHelper.GetUnityAssetType(extension);

            if(m_AssetTypeMap.TryGetValue(SelectedFilter, out var assetType))
            {
                return type == assetType;
            }

            return false;
        }
    }
}
