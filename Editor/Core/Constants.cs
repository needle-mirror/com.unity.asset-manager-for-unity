using System;
using System.Collections.Generic;

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
            { MyAssetsCategoryName, "Downloads.png" },
            { ClosedFoldoutName, "Folder-Closed.png" },
            { OpenFoldoutName, "Folder-Open.png" },
            { ProjectIconName, "Project-Icon.png" },
            { ExternalLinkName, "External-Link.png" }
        };

        public const string AllAssetsFolderName = "All Assets";
        public const string AssetsFolderName = "Assets";
        public const string ApplicationFolderName = "Asset Manager";

        public const string BrowseCategoryName = "Browse";
        public const string MyAssetsCategoryName = "My Assets";
        public const string LocalFilesCategoryName = "Local Files";
        public const string ClosedFoldoutName = "Filters";
        public const string OpenFoldoutName = "Open Folder";
        public const string ProjectIconName = "Project Icon";
        public const string ExternalLinkName = "External Link";

        public const string CategoriesScrollViewUssName = "categories-scrollView";

        public const int DefaultPageSize = 25;

        public const string ThumbnailFilename = "unity_thumbnail.png";

        // This exists here for compatibility with 2020.x versions
        public static DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0);

        // AssetDetailsView Import action text
        public const string ImportActionText = "Import";
        public const string ReimportActionText = "Reimport";
        public const string RemoveFromProjectActionText = "Remove From Project";
        public const string CancelImportActionText = "Cancel Import";
        public const string ShowInProjectActionText = "Show In Project";
        public const string ShowInDashboardActionText = "Show In Dashboard";

        public const string ImportingText = "Importing";

        public const string RemoveFromProjectButtonDisabledToolTip = "There is nothing to remove from the project.";
        public const string ImportButtonDisabledToolTip = "There is nothing to import.";

        public const string CacheThumbnailsFolderName = "Thumbnails";
        public const string CacheTexturesFolderName = "Textures";
        public const string AssetManagerCacheLocationFolder = "AssetManager";
        public const string PackageName = "com.unity.asset-manager-for-unity";

        // Grid View
        public const string GridViewStyleClassName = "grid-view";
        public const string GridViewRowStyleClassName = GridViewStyleClassName + "--row";
        public const string GridViewDummyItemUssClassName = GridViewStyleClassName + "--item-dummy";
        public const string GridItemStyleClassName = "grid-view--item";
        public const string EmptyCollectionsText = "This collection has no assets, use the Asset Manager dashboard to link your assets to a collection.";
        public const string EmptyInProjectText = "Your imported assets will be shown here.";
        public const string EmptyProjectText = "It seems you don't have any assets uploaded to the selected project in your Asset Manager Dashboard.";
        public const string EmptyAllAssetsText = "It seems you don't have any assets uploaded to the selected organization in your Asset Manager Dashboard.";
        public const int ShrinkSizeInMb = 200;
        public const int DefaultCacheSizeGb = 2;
        public const int DefaultCacheSizeMb = DefaultCacheSizeGb * 1024;
    }
}
