using System.IO;
using System.Linq;

namespace Unity.AssetManager.Core.Editor
{
    static class BaseAssetDataExtensions
    {
        public static bool HasImportableFiles(this BaseAssetData assetData)
        {
            return assetData?.GetFiles()?.Any(f =>
                !string.IsNullOrEmpty(f?.Path)
                && !AssetDataDependencyHelper.IsASystemFile(Path.GetExtension(f.Path))) ?? false;
        }
    }
}
