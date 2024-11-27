# Upload assets to Asset Manager

### How to upload assets to Asset Manager from the Unity Editor.

---

The Asset Manager for Unity package enables you to upload assets to Asset Manager. You can use this feature to:

- Upload all related asset files and their dependencies to preserve the assets’ data integrity.
- Share and reuse your game ready assets between different Unity projects and team members. This helps you reduce the cost of asset creation for your project and get the maximum value for an asset.
- Easily manage asset dependencies between Unity editor and Unity Cloud.

> **Important**:
Before you upload assets to Asset Manager, make sure you meet the [Prerequisites](prerequisites.md).

## Upload assets to Cloud

To upload your assets from your local project in the Unity Editor to the Asset Manager, follow these steps:

1. Open the Unity Editor.
2. Go to the Project window, select the assets you want to upload.
3. Right-click and select **Upload to Asset Manager** or drag and drop from Project Window to Asset Manager.
4. Go to the left navigation bar. Select the project you want to upload the assets to from the dropdown list.
5. You can also select the [collection](https://docs.unity.com/cloud/en-us/asset-manager/basic-concepts#collections) you want to upload your asset to. See the **Create a Collection** section for instructions on how to create a collection.
6. In the bottom right corner, you will find the **Upload Settings** window where you can choose one of the following options for the **Reupload mode** setting:
   - **Skip Identical:** Skip assets that are already uploaded to the Asset Manager.
     - **Force New Version:** Create a new version for existing assets with updated versions.
     - **Force New Asset:** Create copies of the assets in Asset Manager.
7. In the same window, you can manage the way dependencies are uploaded by selecting one of the following options for the **Dependencies** setting:
    - **Ignore:** To ignore dependencies.
    - **Separate:** To upload dependencies as separate cloud assets.
    - **Embedded:** To upload dependencies together with the main asset as a single asset.
8. In the same window, you can manage the way paths are displayed by selecting one of the following options for the **File paths** setting:
   - **Full:** Keeps the path relative to the project Assets folder.
   - **Compact:** Reduces files nesting by removing common path parts.
   - **Flatten:** Flatten all files to the root of the asset and rename them in case of collision.
8. Select **Upload assets**.

> **Note**:
Check the asset import states to know the current import and version status of the asset. For more information on asset import states, see Asset import states.

> **Note**:
You can check the version and status of each dependency at upload time by looking at the items under the **Dependencies** foldout of the **Asset Details Page**.

## Create a Collection

To create a collection in a project, follow these steps:

1. Right-click on the project that you want to create a collection for.
2. Select the **Create new collection** option.
3. Enter a name for your new collection.
4. Press the "Enter" key or click away to confirm your selection

You can then rename and delete your collection by right-clicking it and selecting the desired action.
