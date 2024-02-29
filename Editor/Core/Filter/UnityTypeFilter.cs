using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class UnityTypeFilter : LocalFilter, ISerializationCallbackReceiver
    {
        public override string DisplayName => "Type";
        public List<string> m_Selections = new();
        Dictionary<string, UnityAssetType> m_AssetTypeMap;

        public UnityTypeFilter(IPage page)
            : base(page)
        {
            m_AssetTypeMap = new Dictionary<string, UnityAssetType>();

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
            if (m_AssetTypeMap == null || SelectedFilter == null)
                return true;

            await assetData.ResolvePrimaryExtensionAsync(null);
            var type = AssetDataTypeHelper.GetUnityAssetType(assetData.primaryExtension);

            if(m_AssetTypeMap.TryGetValue(SelectedFilter, out var assetType))
            {
                return type == assetType;
            }

            return false;
        }

        public void OnBeforeSerialize()
        {
            // Do nothing
        }

        public void OnAfterDeserialize()
        {
            m_AssetTypeMap = new Dictionary<string, UnityAssetType>();

            var types = (UnityAssetType[])Enum.GetValues(typeof(UnityAssetType));
            foreach (var type in types)
            {
                var text = type.ToString().PascalCaseToSentence();
                m_AssetTypeMap.Add(text, type);
            }
        }
    }
}
