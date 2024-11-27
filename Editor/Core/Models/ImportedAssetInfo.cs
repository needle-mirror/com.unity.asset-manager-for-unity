using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    [Serializable]
    class ImportedAssetInfo
    {
        [SerializeReference]
        public BaseAssetData AssetData;

        public List<ImportedFileInfo> FileInfos;

        public ImportedAssetInfo(BaseAssetData assetData, IEnumerable<ImportedFileInfo> fileInfos)
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

        public static string ToJson(BaseAssetData assetData, IEnumerable<ImportedFileInfo> importedAssetInfo)
        {
            var trackedData = new ImportedAssetInfo(assetData, importedAssetInfo);
            return JsonUtility.ToJson(trackedData, true);
        }
    }
}
