using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class ImportedAssetInfo
    {
        [SerializeReference]
        public IAssetData AssetData;

        public List<ImportedFileInfo> FileInfos;

        public ImportedAssetInfo(IAssetData assetData, IEnumerable<ImportedFileInfo> fileInfos)
        {
            AssetData = assetData;
            FileInfos = fileInfos.ToList();
        }

        public AssetIdentifier Identifier => AssetData?.Identifier;

        public static ImportedAssetInfo Parse(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<ImportedAssetInfo>(jsonString);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string ToJson(IAssetData assetData, IEnumerable<ImportedFileInfo> importedAssetInfo)
        {
            var trackedData = new ImportedAssetInfo(assetData, importedAssetInfo);
            return JsonUtility.ToJson(trackedData, true);
        }
    }
}
