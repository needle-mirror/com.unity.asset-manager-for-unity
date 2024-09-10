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

To upload your project assets from the Unity Editor to Asset Manager, follow these steps:

- Open the Unity Editor.
- Go to the Project window, select the assets you want to upload.
- Right-click and select **Upload to Asset Manager** or drag and drop from Project Window to Asset Manager.
- Go to the left navigation bar. Select the project you want to upload the assets to from the dropdown list.
- You can also select the [collection](https://docs.unity.com/cloud/en-us/asset-manager/basic-concepts#collections) you want to upload your asset to. This is optional.
- In the top right corner, select the gear icon and choose one of the following upload modes:
    - **Ignore already uploaded assets:** Skip assets already uploaded to Asset Manager in Unity Cloud.
    - **Override existing assets:** Replace the existing assets with updated versions.
    - **Duplicate existing assets:** Create copies of the assets in Asset Manager.
- To upload assets and their dependencies, select the gear icon and do one of the following:
    - To upload as a single asset, enable the **Embed dependencies** checkbox.
    - To upload as separate cloud assets, disable the **Embed dependencies** checkbox.
- Select **Upload assets**.

> **Note**:
Check the asset import states to know the current import and version status of the asset. For more information on asset import states, see Asset import states.
