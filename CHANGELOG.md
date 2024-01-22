# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

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