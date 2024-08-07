using System.Collections.Generic;

namespace Unity.AssetManager.Editor
{
    class AssetDataFileComparerByExtension : IComparer<IAssetDataFile>
    {
        public int Compare(IAssetDataFile file1, IAssetDataFile file2)
        {
            return AssetDataTypeHelper.GetPriority(file1?.Extension).CompareTo(
                    AssetDataTypeHelper.GetPriority(file2?.Extension));
        }
    }
}