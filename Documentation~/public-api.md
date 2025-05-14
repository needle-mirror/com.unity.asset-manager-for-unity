# AssetManager for Unity API

### Using the Asset Manager API

---

## How do I automate the import of assets?

The Asset Manager API provides a way to automate the import of assets. You can use the API to create a custom import process that fits your needs.

The import operation takes 3 parameters:
- **ImportSearchFilter**: A set of properties that identify which assets should be targeted for import.
- **ImportSettings**: A set of properties which specify options for the import.
- **CancellationToken** (optional): An optional token to cancel the request.

The import operation returns an `ImportResult` object that contains the list of asset ids that were imported.

>> **Important**: Import operations cannot be run in parallel. If you try to run multiple import operations at the same time, including those started in the UI, the API will throw an exception.

### Importing specific assets

The Asset Manager API provides a way to import specific assets. You can use the `ImportSearchFilter` class to specify the ids of the assets you want to import:

[!code-cs [behaviour-script](../Samples/Documentation/Manual/PublicApiUsage.cs#Example_Import_SpecifyAssets)]

### Importing assets by filter

You can also use the `ImportSearchFilter` class to specify a filter that identifies which assets should be imported. The filter can be based on the asset's properties, such as its type, status, or tags.

In the example below, we use the `ImportSearchFilter` class to specify a filter that identifies all assets in the given project with tags `Texture2D` or `AudioClip`.

[!code-cs [behaviour-script](../Samples/Documentation/Manual/PublicApiUsage.cs#Example_Import_SearchFilter)]

#### How do I find my project id?

If you have linked your project in the **Project Settings**, follow these steps:

1. Go to the top left menu bar, select **Edit** > **Project Settings...**. The Project Settings window opens.
2. From the left menu, select **Services**. The Services General Settings will appear.
3. Below the **Unity Project ID** field, you will see your project id.

If you have linked a different project in the **Project Settings**, follow these steps:

1. Go to the top left menu bar, select **Window** > **Asset Manager**. The Asset Manager window opens.
2. From the right of the Window menu bar, select the context menu button. A context menu opens.
3. Select **Go to Dashboard**. The Asset Manager dashboard opens in your browser.
4. In the left menu, select your project. The project assets will appear.
5. In the browser address bar, you will see the project id in the URL. The project id is the part of the URL that comes after `projects/` and before `/assets`. For example, in the URL `https://cloud.unity.com/home/organizations/1234567890000/projects/00000000-0000-0000-0000-000000000000/assets`, the project id is `00000000-0000-0000-0000-000000000000`.

### Advanced search filtering with the Unity Cloud Assets

If the Asset Manager API does not provide the search capabilities you need, you can use the Unity Cloud Assets search filtering to create an advanced search filter.
You can then gather the assets retrieved from this search and use them in your custom import process.

[!code-cs [behaviour-script](../Samples/Documentation/Manual/PublicApiUsage.cs#Example_Import_AdvancedSearchFilter)]

**Note**: The Unity Cloud Assets is a separate package that provides advanced search capabilities for assets in Unity Cloud. It is not included in the Asset Manager for Unity package. You need to install it separately to use its features.

For more information on how to install the Unity Cloud Assets, see [Install Unity Cloud Assets](https://docs.unity3d.com/Packages/com.unity.cloud.assets@1.7/manual/installation.html).

For more details on how to use the Unity Cloud Assets search filtering, see [Search assets in a project](https://docs.unity3d.com/Packages/com.unity.cloud.assets@1.7/manual/use-case-search-assets.html) and [Search assets across projects](https://docs.unity3d.com/Packages/com.unity.cloud.assets@1.7/manual/use-case-search-across-projects-assets.html).