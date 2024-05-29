# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.7.2] - 2024-05-29

[Fixed]
* Fixed Not Found error while uploading

## [0.7.1] - 2024-05-24

[Fixed]
* Fixed Import Settings
* Running transformation causing upload to fail and not being tracked as imported

### Known Issues
* Failing assertion on import settings path on Mac
* Asset uploaded and tracked without being frozen properly because of transformation will most likely display the "out of date" status right after uploading is finished. This is due to transformation and preview modifying the date of the asset.

## [0.7.0] - 2024-05-17

[Added]
* A new preprocessing directive "AM4U_ENABLE_ASSERTIONS" that enable assertion when set.

[Changed]
* Upload mode set to Override by default
* Show In Project will ping the first file instead of the parent folder
* Draft assets will display their version in yellow like on the web app
* Disabling Remove button while re-import is in progress
* Remove the ImportOperation.ImportAsync IAsset parameter, which was a duplicate of the IAssetData given in the constructor
* Tracking recognizes version changes and out-of-date assets will be updated to the latest version on import
* Cache location is now displayed with an absolute path in the user settings
* Removed download progress indicators from the import operation
* Upload progress indicators are no longer stick
* Improved offline mode and domain reload support

[Fixed]
* Import to action wasn't working
* Don't show import progression of -100% anymore
* Imported asset without .meta file are now properly re-uploaded (need to be imported first)
* Re-uploading a new version if an asset was not tracked properly
* Upload progress not displaying error if details panel is opened
* Normalizing path's separators in AM4U settings
* Fixed Upload files displaying Assets/ folder
* Fixed critical issue where Role and Permissions where not serialized properly
* Fixed error when cancelling Import To folder selection
* Fixed error when selecting Assets folder using Import To dialog
* Removing unused constant causing warning
* Improved search to better support underscores and other separator
* Fixed missing Reveal In Finder label in settings
* Asset files occasionally failing to re-import and being deleted.
* Fixed the import of files with no extensions
* Wrong search results when adding search words while the grid view has not finished loading.
* Fixed a bug where upload progress indicators would be duplicated.

## [0.6.0] - 2024-04-26

[Added]
* It is now possible to bulk import or bulk remove by selecting in the grid view and right click on the selection
* Added new "Default import location" setting in Preferences/Asset Manager
* Added "Import to" option when clicking the Import button
* When "Create by" and "Last edit by" chip are clicked, a corresponding filter is now created
* Show user role

[Changed]
* Ignored upload item are not less dark
* Encode in the .am4u_dep file the proper asset version
* Improved progress reporting
* Username selections in "Create by" and "Update By" are now order in alphabetic order.
* Changed cursor style when hovering over Role chip and Details panel's dashboard link

[Fixed]
* Fixed reimport by cleaning-up moved files correctly
* Asset details panel showed file sizes in base 1024. Now it is harmonized with the web version and it is base 1000.
* Collection hierarchy not correctly reconstruct
* Go To Dashboard when in All Assets page open now the right web page.
* Fix double link icon while loading in Upload page
* Fixed the retrieval of asset that was looking for asset version "1", which does not exists. Use the oldest asset version.
* Fix Add Filter availability when all filter type are used
* Update of the GridItem when finish importing
* Refactored progress code to prevent hangs when importing a large number of files
* When opening the window the first time, permissions wasn't initialized properly

## [0.5.2] - 2024-04-26

### Fixed 
* Fixed for Versioning API breaking uploads

## [0.5.1] - 2024-04-03

### Added
* Displaying subshader graph icons

### Fixed 
* Fixed Details Page Scrollview issues
* Fix detail panel close button overlapping
* Filter visibility when window is too tight
* Disable filter dropdown when there is no asset visible in the current page
* Fixed Loading when entering play mode
* Removed "All Assets" ProjectInfo object that was used by default.
* Fixed missing dependencies foldout bug
* Upload support for asset with circular dependencies
* Fixed a bug that was preventing project selection

## [0.5.0] - 2024-03-15

### Added
* Asset uploading to Unity Cloud
* Analytics to improve user experience
* Support for Unity 2023.3
* Support for opening assets from the Unity Cloud Dashboard

### Changed
* Static placements of UI elements on the Asset Details page
* Context menu for Assets
* Icons for state of Assets

### Fixed 
* Window code optimization improvements
* Loading more than 100 assets may result in unexpected behaviour
* Slow HTTP connections
* Import progress
* HTTP errors not being revealed in console
* "Open the Asset Manager Dashboard" link does not link to the specific project
* Sometimes clicking on project does not register
* Inactive "Remove From Project" button
* Highlighting and focused state colours being the same
* Thumbnails not being updaated properly
* Updating and re-importing assets may cause data sync issues

### Known Issues
* Uploading and downloading in the same project may cause issues
* In-Project view may behave incorrectly
* Cannot cancel imports
* Uploading lighting data from a Scene causes a crash

## [0.4.4] - 2024-02-29

### Changed
* Look and feel of Asset Details page
* Removed re-import tooltip
* Hid description and tag when empty
* Fixed import status not updating after a re-import
* Removed Project dashboard link

### Fixed 
* Window code improvements
* Multiple tags in detail view were stacked in column instead of row
* "Open the Asset Manager Dashboard" not correctly linking to current empty project

### Known Issues
* Updating and re-importing assets may cause data sync issues
* Loading more than 100 assets may result in unexpected behaviour


## [0.4.3] - 2024-02-16

### Added
* First draft of Imported Status
* Support for corrupted/deleted/forbidden imported asset
* Displaying Asset ID in Detail page view
* Filter by Unity type

### Changed
* Look and feel of GridItem highlight and selection is now more clear
* Previously imported assets will not be display in the In Project page and have to be re-imported
* Imported assets are now displayed directly instead of making a request to the backend
* Display Unity Type instead of Dashboard Asset Type
* Display extension when hovering on the Type icon

### Fixed 
* GridItem highlight now behaves properly
* Unintended window refreshing
* Cases where a GridItem shows the wrong Asset Type
* In Project Page not loading if an asset was deleted
* Forbidden error that was showing sometime when after importing an asset
* Add "Loading ..." text in dropdown popup before the selections show up

### Known Issues
* Updating and re-importing assets may cause data sync issues
* Thumbnails may nto be in sync with cloud
* Files are not tracked when moved
* Loading more than 100 assets may result in unexpected behaviour


## [0.4.2] - 2024-02-09

### Fixed 
* Fixed parallel downloads
* Projects are now sorted by name
* Preview files are now ignored
* Meta file are now displayed and downloaded properly

### Added
* Added infinite loading bar when fetching files download URL
* Added "All Assets" option to allow a search over all assets of an organization
* Added link to the Project in the Unity Dashboard
* Project's chip now contains the project's icon

### Changed
* Optimized HTTP calls
* Project dropdown has been changed in favor of a tree view from project to collection

### Known Issues
* "Show In Project" is not working as expected
* “Imported” icon on the file list does not show for meta files
* Loading more than 100 assets may result in unexpected behaviour

## [0.4.0] - 2024-01-22

### Fixed 

* Vertically centered the listed file items in the details page

### Added

* Warning box in the details page when there are no files associated with an asset
* Refresh menu item in the window contextual menu that refreshes projects, collections and assets

### Changed

* Initial gridview loading screen no longer shows blank grid items with loading icons


## [0.3.0] - 2024-01-03

### Fixed

* Errors caused by selecting a cache location without write permissions
* Fixed forever loading screen when organisation was not set at first launch.
* Fixed message shown when search result is empty
* Fixed forever loading page when entering play mode before page was fully loaded
* Fixed null exception when removing selected asset from in project
* Fixed details page closing when removing an asset in project that is not selected
* Don't see .meta and .DS_Store in the included file.
* Fixed import of assets with special characters

### Added

* Explanatory tooltips to disabled buttons in the Asset Details Page

### Changed

* Updated versions of com.unity.cloud packages
* Default import location path
* Show in project button now highlights the asset's file in the project browser
* Search now fetches the asset and it's files all in one call
* Remove the id in the folder name after importing and manage import name conflict

## [0.2.0] - 2023-12-05

### Added

* Message to show when they have no assets in the project and a link to the dashboard
* Message to show when Organisation has no projects and a link to the dashboard

### Fixed

* Fixed Editor namespaces being used in package which was colliding with URP and HDRP projects
* Fixed height of the thumbnail in the details view
* Collections expanded/collapsed values now survive domain reload
* Fixed Included Files initial size being too small
* Fixed file information loading forever for some assets
* Made sure we got the full project list

### Changed

* "No results found" text now shows a more descriptive message depending on the page/results

## [0.1.1] - 2023-11-16

### Changed

* Moved the Resources folder under the Editor folder so they are not part of the users' builds

## [0.1.0] - 2023-11-14

### Added

* Cancelable import progress bar has been added to the grid items and asset detail page
* Refresh button next to the search bar
* Error message for when an organization is not selected in the `Project Settings -> Services` section
* Links to the Dashboard, Project Settings and Preferences
* Next page loading icon has a loading bar with informative text
* Grid item corner icon status
* Context menu actions
* Show in project allows users to quickly find the file in their project
* In Project feature allows users to see which assets are in their projects
* Remove from project
* Import into project
* Search bar functionality
* Cache management feature allows users to manage and clear their cache
* Cache settings allows users to set the cache size limit and location of their cache
* Breadcrumbs
* Project selection dropdown allows users to select which project they want to search in
* Inspector section for the asset details page
* Collections
* Grid items
