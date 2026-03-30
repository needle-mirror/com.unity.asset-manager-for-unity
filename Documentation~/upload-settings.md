# Upload settings panel

### Explore the options in the upload settings panel 
---
Use the upload options to control how Asset Manager organizes assets, handles dependencies, and manages existing files during upload. Configure these settings before you upload to make sure Asset Manager stores assets the way you want.

| Option | Function |
|----------|----------|
| **Match project structure** | Select this checkbox to organize your assets into collections to match your Unity project folder structure. When you enable this option: <br><br> - It groups assets under folders that mirror your project's directory hierarchy. For example, `Assets/Textures/Characters/` becomes `Textures/Characters/`. <br> - The **Embedded** dependency mode is unavailable. |
| **Reupload mode** | Select how to handle existing assets during reupload. The options are: <br><br> - **Skip Identical**: Skip assets that already exist in Asset Manager. <br> - **Force New Version**: Create new versions for existing assets. <br> - **Force New Asset**: Create copies of the existing assets in Asset Manager. |
| **Dependencies** | Select how to handle dependencies for the assets you upload. The options are: <br><br> - **Ignore**: Ignore dependencies. <br> - **Separate**: Upload dependencies as separate cloud assets. <br> - **Embedded**: Upload dependencies with the main asset as a single asset. |
| **File path** | Select how to display file paths. The options are: <br><br> - **Full**: Keep the path relative to the project `Assets` folder. <br> - **Compact**: Remove common path parts to reduce file nesting. <br> - **Flatten**: Move all files to the asset root and rename duplicates if a naming conflict occurs. |

> **Note**:
> - The **Full** file path option preserves the asset folder hierarchy during import. The **Compact** and **Flatten** options import assets into the root `Assets` folder.

> **Important**:
> **Match project structure**
> - This workflow supports only single-file assets, such as `.fbx`, `.png`, or `.mat` files. It doesn't support multi-file assets.
> - When you reupload an existing asset, it also [links](https://docs.unity.com/en-us/cloud/asset-manager/asset-action-menu#menu-actions) the asset to the selected collection, whether the collection already exists or is new. The upload staging area shows the linked assets in the collection tree and notifies you about the linking behavior. After uploading, to remove the link in Asset Manager web, select **Remove from Collection**.
> - If you select **Separate** to handle dependencies, you can ignore specific dependencies in the upload staging area. To ignore dependencies one at a time, clear the checkbox or right-click the dependency and select **Ignore**.
> - Moving assets in Asset Manager web changes only the asset organization there. It doesn't update your local Unity Editor project, which can create a mismatch between the two locations.






