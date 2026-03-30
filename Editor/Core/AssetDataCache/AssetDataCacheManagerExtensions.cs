namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Extension methods for AssetDataCacheEntry.
    /// </summary>
    static class AssetDataCacheEntryExtensions
    {
        /// <summary>
        /// Returns an AssetIdentifier for this entry, for use with IAssetsProvider (e.g. refresh).
        /// Returns null if required identifier fields are missing.
        /// </summary>
        public static AssetIdentifier ToAssetIdentifier(this AssetDataCacheEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.assetId))
                return null;

            var id = new AssetIdentifier(entry.organizationId, entry.projectId, entry.assetId, entry.versionId ?? string.Empty, entry.versionLabel ?? string.Empty);
            if (!string.IsNullOrEmpty(entry.libraryId))
                id.LibraryId = entry.libraryId;
            return id;
        }
    }
}
