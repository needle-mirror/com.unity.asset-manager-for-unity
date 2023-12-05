using System;
using System.Collections.Generic;
using UnityEditor;

namespace Unity.AssetManager.Editor
{
    internal static class Constants
    {
        // Categories View
        public static readonly Dictionary<string, string> CategoriesAndIcons = new()
        {
            { AllAssetsFolderName, "All-Assets.png" },
            { LocalFilesCategoryName, "Local-Files.png" },
            { BrowseCategoryName, "Browse-Assets.png" },
            { InProjectTabName, "Folder-Closed.png" },
            { MyAssetsCategoryName, "Downloads.png" },
            { ClosedFoldoutName, "Folder-Closed.png" },
            { OpenFoldoutName, "Folder-Open.png" },
            { ImportedFolderName, "Folder-Closed.png" },
        };

        public const string AllAssetsFolderName = "All Assets";
        public const string AssetsFolderName = "Assets";
        public const string ApplicationFolderName = "Asset Manager";
        public const string ImportedFolderName = "Imported";

        public const string BrowseCategoryName = "Browse";
        public const string MyAssetsCategoryName = "My Assets";
        public const string LocalFilesCategoryName = "Local Files";
        public const string ClosedFoldoutName = "Filters";
        public const string OpenFoldoutName = "Open Folder";

        public const string InProjectTabName = "In Project";

        public const string CategoriesScrollViewUssName = "categories-scrollView";

        public const int DefaultPageSize = 25;

        // This exists here for compatibility with 2020.x versions
        public static DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0);

        // AssetDetailsView Import button text
        public const string ImportText = "Import";
        public const string ImportingText = "Importing";
        public const string ReImportText = "Re-Import";

        public const string CacheThumbnailsFolderName = "Thumbnails";
        public const string CacheTexturesFolderName = "Textures";
        public const string AssetManagerCacheLocationFolder = "AssetManager";
        public const string PackageName = "com.unity.asset-manager-for-unity";

        // Context menu
        public const string ContextMenuImport = "Import";
        public const string ContextMenuRemoveFromLibrary = "Remove From Project";

        // Grid View
        public const string GridViewStyleClassName = "grid-view";
        public const string GridViewRowStyleClassName = GridViewStyleClassName + "--row";
        public const string GridViewDummyItemUssClassName = GridViewStyleClassName + "--item-dummy";
        public const string GridItemStyleClassName = "grid-view--item";
        public const string EmptyCollectionsText = "This collection has no assets, use the Asset Manager dashboard to link your assets to a collection.";
        public const string EmptyInProjectText = "Your imported assets will be shown here.";
        public const string EmptyAllAssetText = "It seems you don't have any assets uploaded to the selected project in your Asset Manager Dashboard.";
        public const int ShrinkSizeInMb = 200;
        public const int DefaultCacheSizeGb = 2;
        public const int DefaultCacheSizeMb = DefaultCacheSizeGb * 1024;
    }
}
