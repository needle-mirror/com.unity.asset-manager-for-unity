using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class ImportedAssetInfo
    {
        [SerializeReference]
        public IAssetData assetData;
        public AssetIdentifier id => assetData?.identifier;
        
        public List<ImportedFileInfo> fileInfos;
        
        public ImportedAssetInfo(IAssetData assetData, IEnumerable<ImportedFileInfo> fileInfos)
        {
            this.assetData = assetData;
            this.fileInfos = fileInfos.ToList();
        }
    }
}
