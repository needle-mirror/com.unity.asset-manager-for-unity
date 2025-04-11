# User Preferences

### Managing Asset Manager Editor Preferences.

---

## How do I change the Asset Manager Editor preferences?

To change the Asset Manager Editor preferences, follow these steps:

1. In the Unity Editor, select **Edit** > **Preferences** (macOS: **Unity** > **Settings...**) from the main menu.
2. In the **Preferences** window, select **Asset Manager**.

## Import Settings

### Default import location

The default import location is the folder where the Asset Manager imports assets. By default, this is the **Assets** folder in your project.
You can change this to any folder in your project.

### Create subfolder on import

When enabled, the Asset Manager will create a subfolder in the default import location named after the asset.

### Avoid rolling back versions of dependencies

When enabled, the Asset Manager will not roll back versions of dependencies when importing an asset.
This is useful if you want to keep the current version of a dependency in your project.

### Disable the reimport modal

When enabled, the Asset Manager will not show the reimport dialog when importing an asset.
This is useful if you want to import assets without being prompted for confirmation.

> **Important**:
> The reimport modal can only be disabled if the **Avoid rolling back versions of dependencies** option is enabled.

> **Note**:
> If you disable the reimport modal, you will not be able to select which files to reimport. The Asset Manager will automatically replace all files of the asset(s) being imported.

## Cache Settings

### Cache location

The cache location is the folder where the Asset Manager stores cached assets. By default, this is the default **Unity/cache** folder found within your user profile. You can change this to any folder on your machine.

### Maximum cache size

The maximum cache size is the maximum amount of disk space that the Asset Manager will use to store cached assets. By default, this is set to 2 GB. You can change set it to any value between 2 GB and 200 GB.

## Upload Settings

### Generate tags automatically based on preview image

When enabled, the Asset Manager will automatically generate AI tags for assets based on the preview image.

### Confidence level for automatic tag generation

The confidence level for automatic tag generation is the minimum confidence level that the Asset Manager will use to generate tags for assets based on the preview image. By default, this is set to 80%. You can change this to any value between 0 and 100.

### Upload dependencies with **Latest** version label

When enabled, the dependencies of an uploaded asset will point to the version with the **Latest** version label. When disabled, the uploaded asset will point to fixed versions of its dependencies.