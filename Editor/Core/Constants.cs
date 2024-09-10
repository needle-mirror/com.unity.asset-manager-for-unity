using System;

namespace Unity.AssetManager.Editor
{
    static class Constants
    {
        public const string AllAssetsFolderName = "All Assets";
        public const string AssetsFolderName = "Assets";
        public const string ApplicationFolderName = "Asset Manager";

        public const string CategoriesScrollViewUssName = "categories-scrollView";

        public const int DefaultPageSize = 50;

        // Sort
        public const string Sort = "Sort by:";

        // Tabs
        public const string AssetsTabLabel = "Assets";
        public const string InProjectTabLabel = "In Project";
        public const string UploadTabLabel = "Upload";

        // Upload
        public const string IgnoreAll = "Ignore All";
        public const string IncludeAll = "Include All";
        public const string IgnoreAsset = "Ignore Asset";
        public const string IncludeAsset = "Include Asset";
        public const string IgnoreAssetToolTip = "This asset is ignored and will not\nbe uploaded to the Asset Manager.";
        public const string IgnoreToggleTooltip = "Uncheck to ignore asset";
        public const string IncludeToggleTooltip = "Check to include asset";
        public const string IgnoreDependenciesDialogTitle = "Warning";
        public const string IgnoreDependenciesDialogMessage = "You are trying to upload assets without their dependencies. This might break other assets that depend on them.\nAre you sure you want to proceed?";
        public const string UploadChangelog = "Asset Manager Upload";
        public const string UploadNoAssetsMessage = "Drag and drop assets from the Project window\n\nor\n\nIn the Project window, right-click on a file or folder and select Upload to Asset Manager";
        public const string CancelUploadActionText = "Cancel Upload";
        public const string ClearAllActionText = "Clear All";
        public const string UploadActionText = "Upload Assets";
        public const string UploadingText = "Uploading...";
        public const string UploadNoEntitlementMessage = "You can't upload this asset without an assigned seat. \nContact your Organization Owner to assign you a seat.";
        public const string UploadNoPermissionTooltip = "You don’t have permissions to upload this asset. \nSee your role from the project settings page on \nthe Asset Manager dashboard.";
        public const string UploadCloudServicesNotReachableTooltip = "Cloud services are not reachable";
        public const string UploadAllIgnoredTooltip = "All assets are ignored";
        public const string UploadNoProjectSelectedTooltip = "Select a project to upload assets";
        public const string UploadNoAssetsTooltip = "No assets to upload";
        public const string UploadWaitStatusTooltip = "Waiting for assets status...";
        public const string UploadAssetsTooltip = "Upload assets to cloud";
        public const string UploadAssetsExistsTooltip = "All assets already exist in the cloud";
        public const string UploadOutsideProjectTooltip = "One of more assets contain files outside the Assets folder";

        // Preview Status
        public const string ImportedText = "Asset is imported";
        public const string UpToDateText = "Asset is up to date";
        public const string OutOfDateText = "Asset is outdated";
        public const string StatusErrorText = "Asset was deleted or is not accessible";

        // Upload Status
        public const string LinkedText = "This asset is a dependency of another asset";
        public const string UploadAddText = "This asset does not exist on the cloud and will be added";
        public const string UploadSkipText = "This asset already exists on the cloud and will not be uploaded";
        public const string UploadOverrideText = "This asset will override its cloud version";
        public const string UploadDuplicateText = "This asset already exists on the cloud but a new cloud asset will be uploaded";
        public const string UploadOutsideText = "This asset is outside the Assets folder and cannot be uploaded";

        // AssetDetailsView Asset info
        public const string AssetIdText = "Asset Id";
        public const string VersionText = "Ver. ";
        public const string PendingVersionText = "Pending Ver.";
        public const string FromVersionText = "From Ver.";
        public const string DashboardLinkTooltip = "Open asset in the dashboard";
        public const string TotalFilesText = "Total Files";
        public const string FilesSizeText = "Files Size";
        public const string LoadingText = "Loading...";
        public const string ChangeLogText = "What's changed?";
        public const string NoChangeLogText = "No change log provided.";
        public const string CreatedFromText = "Created From";
        public const string CreatedByText = "Created By";
        public const string CreatedDateText = "Created Date";
        public const string ModifiedByText = "Modified By";
        public const string ModifiedDateText = "Modified Date";
        public const string TagsText = "Tags";
        public const string ProjectText = "Project";
        public const string AssetTypeText = "Asset Type";
        public const string StatusText = "Status";
        public const string SourceFilesText = "Source Files";
        public const string UVCSFilesText = "UVCS Files";
        public const string NoFilesText = "No files were found in this asset.";
        public const string SameFileNamesText = "Files of the same name were found in this asset. Unity does not support files with the same name.";
        public const string DependenciesText = "Dependencies";
        public const string NoDependenciesText = "This asset has no dependencies";
        public const string ServiceAccountText = "Service Account";
        public const string LatestTagText = "Latest";
        public const string ImportedTagText = "Imported";

        // AssetDetailsView Asset status
        public const string AssetDraftStatus = "Draft";
        public const string AssetInReviewStatus = "InReview";
        public const string AssetApprovedStatus = "Approved";
        public const string AssetRejectedStatus = "Rejected";
        public const string AssetPublishedStatus = "Published";
        public const string AssetWithdrawnStatus = "Withdrawn";

        // AssetDetailsView Import action text
        public const string ImportActionText = "Import";
        public const string ImportToActionText = "Import To";
        public const string ImportButtonTooltip = "Imports all associated files of the Cloud Asset into your Unity project";
        public const string UpdateToLatestActionText = "Update To Latest";
        public const string ReimportActionText = "Reimport";
        public const string ReimportButtonTooltip = "Reimports all associated files of the Cloud Asset into your Unity project";
        public const string ImportLocationTitle = "Choose import location";
        public const string RemoveFromProjectActionText = "Remove From Project";
        public const string RemoveAllFromProjectActionText = "Remove All From Local Project";
        public const string CancelImportActionText = "Cancel Import";
        public const string ShowInProjectActionText = "Show In Project";
        public const string ShowInProjectButtonToolTip = "Pings the Asset in an active Project window";
        public const string ShowInProjectButtonDisabledToolTip = "This Asset has not yet been imported";
        public const string ShowInDashboardActionText = "Show In Dashboard";
        public const string AssetsSelectedTitle = "Assets Selected";

        public const string ImportingText = "Importing";
        public const string ImportAllSelectedActionText = "Import All Selected";
        public const string RemoveFromProjectAllSelectedActionText = "Remove All Selected From Project";
        public const string RemoveFromProjectButtonToolTip = "Removes the asset from your Unity project";
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
        public const string EmptyProjectText = "The selected project is empty. To add assets, right click on any asset in project window and select upload to asset manager.";
        public const string EmptyAllAssetsText = "The selected organization is empty. To add assets, right click on any asset in project window and select upload to asset manager.";
        public const int ShrinkSizeInMb = 200;
        public const int DefaultCacheSizeGb = 2;
        public const int DefaultCacheSizeMb = DefaultCacheSizeGb * 1024;

        // Permissions
        public const string ImportPermission = "amc.assets.download";
        public const string UploadPermission = "amc.assets.create";

        // Reimport Window
        public const string ReimportWindowConflictsTitle = "Some files have conflicts. To complete import, choose how to resolve them.";
        public const string ReimportWindowConflictsWarning = "Warning: replacing files will overwrite local copies.";
        public const string ReimportWindowDependentsTitle = "The following dependent assets have updates:";
        public const string ReimportWindowUpwardDependenciesTitle = "The following assets have dependencies on updated assets. They might also be affected:";
        public const string ReimportWindowCancel = "Cancel";
        public const string ReimportWindowImport = "Import";
        public const string ReimportWindowSkip = "Skip";
        public const string ReimportWindowUpdate = "Update";
        public const string ReimportWindowReimport = "Reimport";

        // Asset Manager Settings
        public const string AssetManagerTitle = "Asset Manager";

        public const string ImportSettingsTitle = "Import Settings";
        public const string ImportDefaultLocation = "Default import location";
        public const string ImportCreateSubfolders = "Create subfolder on import";
        public const string ImportCreateSubfoldersTooltip = "Enabling this option will automatically generate a folder named after the cloud asset and import files within it";

        public const string CacheSettingsTitle = "Cache Settings";
        public const string CacheLocationTitle = "Cache location";
        public const string AccessError = "Some folders or files could not be accessed.";
        public const string DirectoryDoesNotExistError = "This directory does not exist";
        public const string RevealInFinder = "Reveal in Finder";
        public const string ShowInExplorerLabel = "Show in Explorer";
        public const string ChangeLocationLabel = "Change Location";
        public const string ResetDefaultLocation = "Reset to Default Location";
        public const string CacheLocation = "Cache Location";
        public const string CacheMaxSize = "Maximum cache size (GB)";
        public const string CacheSize = "Cache size is ";
        public const string CacheRefresh = "Refresh";
        public const string CleanCache = "Clean cache";
        public const string ClearExtraCache = "Clear extra cache";

        public const string UploadSettingsTitle = "Upload Settings";
        public const string TagsCreation = "Generate tags automatically based on preview image";
        public const string TagsCreationConfidenceLevel = "Confidence level for automatic tags generation";
        public const string TagsCreationConfidenceLevelTooltip = "The higher the value, the more accurate the tags will be.";

        // This exists here for compatibility with 2020.x versions
        public static DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0);
    }
}
