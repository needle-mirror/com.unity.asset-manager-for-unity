# Asset Manager

The asset manager is an In-Editor tool that allows you to browse and import individual assets from the Asset Manager service directly into your project. This package will add a new dockable window where assets are displayed and interacted with.

# Known issues

- Re-import does not overwrite previous file
- Asset Manager window stuck in infinite loading if user enter play mode while assets are still loading
- Sometimes, context menu "Import" does nothing when used in an unselected grid item
- Switching project while offline throws errors
- Details page is closed and asset is deselected when deleting a file of the asset while in the "In Project" filter
- Clearing the search does not dismiss details view on previous selected asset
- Importing some assets does not import all of its included files
- In details view, the description text overlaps other item if too long
- Logs are printed in Unity console when using Asset Manager in playmode
- Backspace does not remove search pills
- Unused Settings.json file is created after a domain reload
- Import in context menu should be disabled when the import in detail view is disabled
- Docked window is cutoff and we can't see all the information
- Gridview contents are not updated when switching linked organization
- No more assets toast appears when there are no results in the grid view
- Closing Asset Manager window on Mac leaves project dropdown open
- Some asset cannot be imported due to PathTooLongException