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

        /// <summary>
        /// Whether an import action targeting this asset should be offered to the user.
        /// An asset whose datasets have not been resolved yet has an empty file list, which only means the
        /// files are not known yet. Use this instead of <see cref="HasImportableFiles"/> to drive the enabled
        /// state of import actions, so they are not disabled while the file list is still being resolved.
        /// </summary>
        public static bool MayHaveImportableFiles(this BaseAssetData assetData)
        {
            if (assetData == null)
                return false;

            return !assetData.AreDatasetsResolved || assetData.HasImportableFiles();
        }
    }
}
