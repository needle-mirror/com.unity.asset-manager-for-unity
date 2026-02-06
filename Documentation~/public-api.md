# Asset Manager for Unity API

### Use the Asset Manager API to automate asset import.

---

## Automate asset import

Use the Asset Manager API to automate asset import. You can use the Asset Manager API to create a custom asset import process that fits your needs.

The asset import operation uses three parameters:
- **ImportSearchFilter**: A set of properties that identify which assets to target for import.
- **ImportSettings**: A set of properties which specify options for the import.
- **CancellationToken** (optional): An optional token to cancel the request.

The asset import operation returns an `ImportResult` object that contains the list of asset IDs for the imported assets.

>> **Important**: You can't run multiple asset import operations in parallel. If you try to run multiple import operations, including those started in the UI, the API will throw an exception.

### Import specific assets

The Asset Manager API provides a way to import specific assets. You can use the `ImportSearchFilter` class to specify the IDs of the assets you want to import:

[!code-cs [behaviour-script](../Samples/Documentation/Manual/PublicApiUsage.cs#Example_Import_SpecifyAssets)]

### Import assets by filter

You can also use the `ImportSearchFilter` class to specify a filter that identifies which assets are imported. You can base the filter on the asset properties such as type, status, or tags.

The following example uses the `ImportSearchFilter` class to specify a filter that identifies all assets in a project with the tags `Texture2D` or `AudioClip`.

[!code-cs [behaviour-script](../Samples/Documentation/Manual/PublicApiUsage.cs#Example_Import_SearchFilter)]

#### Find your project ID

If you have linked your project in the **Project Settings**, follow these steps:

1. From the main menu bar, select **Edit** > **Project Settings...**. The Project Settings window opens.
2. In the Project Settings window, select **Services**. The Services General Settings appear.
   The project ID displays under **Unity Project ID**.

If you have linked a different project in the **Project Settings**, follow these steps:

1. Go to the main menu bar, select **Window** > **Asset Manager**. The Asset Manager window opens.
2. In the Asset Manager window, select the context menu button. A context menu opens.
3. Select **Go to Dashboard**. The Asset Manager dashboard opens in your browser.
4. In the left menu, select your project. The project assets appear.
5. In the browser address bar, the project ID appears in the URL after `projects/` and before `/assets`. For example, in the URL `https://cloud.unity.com/home/organizations/1234567890000/projects/00000000-0000-0000-0000-000000000000/assets`, the project ID is `00000000-0000-0000-0000-000000000000`.

### Advanced search filtering with Unity Cloud Assets

If the Asset Manager API doesn't provide the search capabilities you need, you can use Unity Cloud Assets search filtering to create an advanced search filter.
You can gather the assets retrieved from the search results and use them in your custom import process.

[!code-cs [behaviour-script](../Samples/Documentation/Manual/PublicApiUsage.cs#Example_Import_AdvancedSearchFilter)]

**Note**: Unity Cloud Assets is a separate package that provides advanced search capabilities for assets in Unity Cloud. This package is not included in the Asset Manager for Unity package. You must install it separately to use its features.

For more information on how to install the Unity Cloud Assets, see [Install Unity Cloud Assets](https://docs.unity3d.com/Packages/com.unity.cloud.assets@1.7/manual/installation.html).

For more details on how to use the Unity Cloud Assets search filtering, see [Search assets in a project](https://docs.unity3d.com/Packages/com.unity.cloud.assets@1.7/manual/use-case-search-assets.html) and [Search assets across projects](https://docs.unity3d.com/Packages/com.unity.cloud.assets@1.7/manual/use-case-search-across-projects-assets.html).

## Set asset metadata during an upload

Use the Asset Manager API to set asset metadata before upload. This metadata includes the name, description, status, tags, and custom metadata. To do this, write a script that does the following: 
- Create a subclass of the `AssetManagerPostprocessor` class
- In this class, override the `OnPostprocessUploadAsset` method.

The following example demonstrates how to create a custom post-processor that adds a custom tag to material assets during upload.

[!code-cs [behaviour-script](../Samples/Documentation/Manual/PublicApiUsage.cs#Example_Custom_AssetManagerPostprocessor)]

### Details on the OnPostprocessUploadAsset method

For each upload asset, the `OnPostprocessUploadAsset` method performs the following operations:
1. Uses Asset Manager to evaluate all assets for upload, including dependencies.
2. For each identified asset, the Asset Manager calculates default values for the metadata.
3. For each asset, Asset Manager does the following:
    1. Creates an `UploadAsset` object that represents the asset to be uploaded.
    2. Searches for all derivations of the `AssetManagerPostprocessor` type.
    3. Instantiates one object for each found type and orders objects based on their priority value.
    4. For each object from the previous step, call `OnPostprocessUploadAsset`, giving the `UploadAsset` object representing the asset to be uploaded.
4. The upload staging area is populated with the assets to be uploaded, using the metadata from the `UploadAsset` object after all post-processors have been executed.
