using System;
using System.Collections.Generic;

namespace Unity.AssetManager.Editor
{
    static class Constants
    {
        public const string AllAssetsFolderName = "All Assets";
        public const string AssetsFolderName = "Assets";
        public const string ApplicationFolderName = "Asset Manager";

        public const string CategoriesScrollViewUssName = "categories-scrollView";

        public const int DefaultPageSize = 25;

        public const string ThumbnailFilename = "unity_thumbnail.png";

        // Upload
        public const string IgnoreAsset = "Ignore Asset";
        public const string IncludeAsset = "Include Asset";
        public const string IgnoreAssetToolTip = "This asset is ignored and will not\nbe uploaded to the Asset Manager.";
        public const string IgnoreToggleTooltip = "Uncheck to ignore asset";
        public const string IncludeToggleTooltip = "Check to include asset";
        public const string IgnoreDependenciesDialogTitle = "Warning";
        public const string IgnoreDependenciesDialogMessage = "You are trying to upload assets without their dependencies. This might break other assets that depend on them.\nAre you sure you want to proceed?";
        public const string UploadChangelog = "Asset Manager Upload";

        // Preview Status
        public const string ImportedText = "Asset is imported";
        public const string UpToDateText = "Asset is up to date";
        public const string OutOfDateText = "Asset is outdated";
        public const string StatusErrorText = "Asset was deleted or is not accessible";

        // Upload Status
        public const string LinkedText = "This asset is a dependency of another asset";
        public const string UploadSkipText = "This asset already exists on the cloud and will not be uploaded";
        public const string UploadOverrideText = "This asset will override its cloud version";
        public const string UploadDuplicateText = "This asset already exists on the cloud but a new cloud asset will be uploaded";

        // AssetDetailsView Asset info
        public const string VersionText = "Ver. ";
        public const string PendingText = "Pending";

        // AssetDetailsView Asset status
        public const string AssetDraftStatus = "Draft";

        // AssetDetailsView Import action text
        public const string ImportActionText = "Import";
        public const string UpdateToLatestActionText = "Update To Latest";
        public const string ReimportActionText = "Re-import";
        public const string RemoveFromProjectActionText = "Remove From Project";
        public const string RemoveAllFromProjectActionText = "Remove All From Local Project";
        public const string CancelImportActionText = "Cancel Import";
        public const string ShowInProjectActionText = "Show In Project";
        public const string ShowInDashboardActionText = "Show In Dashboard";
        public const string AssetsSelectedTitle = "Assets Selected";

        public const string ImportingText = "Importing";
        public const string ImportAllSelectedActionText = "Import All Selected";
        public const string RemoveFromProjectAllSelectedActionText = "Remove All Selected From Project";

        public const string RemoveFromProjectButtonDisabledToolTip = "There is nothing to remove from the project.";
        public const string ImportButtonDisabledToolTip = "There is nothing to import.";
        public const string ImportNoPermissionMessage = "You don’t have permissions to import this asset. \nSee your role from the project settings page on \nthe Asset Manager dashboard.";

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

        // Permissions
        public const string ImportPermission = "amc.assets.download";
        public const string UploadPermission = "amc.assets.create";

        // Icons
        public const string PackageIcon = "Package-Icon.png";
        public const string ProjectIcon = "Project-Icon.png";

        // This exists here for compatibility with 2020.x versions
        public static DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0);
    }
}
