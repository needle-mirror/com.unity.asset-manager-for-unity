using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    [Serializable]
    class LocalUnityTypeFilter : LocalFilter, ISerializationCallbackReceiver
    {
        public override string DisplayName => "Type";

        Dictionary<string, UnityAssetType> m_AssetTypeMap;
        List<string> m_Selections = new();

        public LocalUnityTypeFilter(IPage page)
            : base(page)
        {
           ResetSelections();
        }

        public void OnBeforeSerialize()
        {
            // Do nothing
        }

        public void OnAfterDeserialize()
        {
           ResetSelections();
        }

        public override Task<List<string>> GetSelections()
        {
            return Task.FromResult(m_Selections);
        }

        public override async Task<bool> Contains(BaseAssetData assetData, CancellationToken token = default)
        {
            if (m_AssetTypeMap == null || SelectedFilters == null)
            {
                return true;
            }

            await assetData.ResolveDatasetsAsync(token: token);
            var type = AssetDataTypeHelper.GetUnityAssetType(assetData.PrimaryExtension);

            foreach (var selectedFilter in SelectedFilters)
            {
                if (m_AssetTypeMap.TryGetValue(selectedFilter, out var assetType) && type == assetType)
                {
                    return true;
                }
            }

            return false;
        }

        void ResetSelections()
        {
            m_Selections = new List<string>();
            m_AssetTypeMap = new Dictionary<string, UnityAssetType>();

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
