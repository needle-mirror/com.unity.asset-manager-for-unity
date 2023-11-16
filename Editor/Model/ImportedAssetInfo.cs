using System;
using System.Collections.Generic;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class ImportedAssetInfo
    {
        public AssetIdentifier id;
        public List<ImportedFileInfo> fileInfos;
    }
}
