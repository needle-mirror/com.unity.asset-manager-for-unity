# Asset Manager

The asset manager is an In-Editor tool that allows you to browse and import individual assets from the Asset Manager service directly into your project. This package will add a new dockable window where assets are displayed and interacted with.

# Known issues

- Re-import does not overwrite previous file
- Switching project while offline throws errors
- In details view, the description text overlaps other item if too long
- Logs are printed in Unity console when using Asset Manager in playmode
- Unused Settings.json file is created after a domain reload
- Docked window is cutoff and we can't see all the information
- Gridview contents are not updated when switching linked organization
- No more assets toast appears when there are no results in the grid view
- Closing Asset Manager window on Mac leaves project dropdown open
- Some asset cannot be imported due to PathTooLongException