# Tracking files

### How Asset Manager for Unity tracks imported cloud assets in your project.

---

Asset Manager for Unity uses tracking files to track the cloud assets and associated files imported into your project. This topic explains tracking files, where they're stored, and how to manage them with version control.

## Location

Asset Manager for Unity stores tracking files in your project at the following location:

```
{Project Folder}/uam/
```

The folder structure inside `uam/` matches the `Assets/` folder structure in your Unity project. Every Unity asset file that belongs to a tracked cloud asset also has its own `.json` tracking file. For example, if you import a cloud asset to `Assets/Models/Car/car.fbx`, Asset Manager for Unity stores a `.json` tracking file at the following location:

```
uam/Models/Car/car.fbx.json
```

## When multiple assets use the same files (overlapping imports)

The Asset Manager cannot represent multiple owners per file in the tracking layer. If you import two or more assets that write files to the same destination path, the Asset Manager creates only one tracking file per path. The tracking file references the last imported asset file.  

For example, if you import two embedded assets that share the same texture and you choose **Replace** when prompted, the tracking file updates its reference to the last imported asset.

**What you'll see in the UI**

- **At import or reimport**: The message `The following files are already tracked by another asset. Their tracking will be overridden` means that the listed files are already tracked by another imported asset. Importing or reimporting these files overwrites tracking information with the current asset.
- **In the Asset Inspector**: The message `Some files in this asset are also tracked by another asset. Their tracking may conflict or be incomplete` means that some of the asset's files are shared with at least one other imported asset.

**Risks when removing an asset**

When multiple imported assets share files, removing one of these assets from the project might delete shared files from disk. The exact behavior depends on the asset that the tracking file references. To avoid losing shared files when removing an asset, do the following:

- Avoid importing different assets to the same destination path.
- Before you select **Remove From Project**, consider which asset needs the shared files. 
- After you remove an asset, refresh the Asset Manager window and re-check the **In Project** list before removing another asset.

For more information, refer to [Remove an imported asset](import-assets.md#remove-an-imported-asset).

## File contents

The Asset Manager package generates and manages the contents of tracking files. Don't manually delete or modify tracking files. Each `.json` tracking file includes the following fields:

| Field | Description |
|-------|-------------|
| `path` | The original cloud or Asset Manager path for the file. |
| `assetName` | The name of the asset. |
| `assetId` | The ID of the asset. |
| `datasetId` | The dataset ID (for assets with multiple datasets.) |
| `projectId` | The Unity Cloud project ID. |
| `organizationId` | The Unity Cloud organization ID. |
| `versionId` | The imported version ID. |
| `sequenceNumber` | The version sequence number. |
| `updated` | The timestamp when the asset was last updated in the cloud. |
| `timestamp` | The file modification timestamp at import time. |
| `checksum` | The MD5 checksum of the file at import time. |
| `metaFileChecksum` | The MD5 checksum of the `.meta` file at import time. |
| `metaFileTimestamp` | The `.meta` file modification timestamp at import time. |
| `unityGUID` | The Unity GUID of the asset file. |

## Version control

It's important to commit the tracking files folder to your version control system (VCS). If you don't commit tracking files, team members will be unable to view and update imported cloud assets in the **In Project** tab.

Committing tracking files has the following advantages:

 * **Team synchronization**: All team members share the same tracking information. All team members view the same import states and can update assets consistently.
 * **Reimport support**: The Asset Manager uses tracking data to detect older cloud assets and asks whether to reimport newer version.
 * **Local change detection**: The Asset Manager uses checksums in tracking files to detect local modification of imported assets.
 * **Consistency after moves**: If you move an asset within a project, tracking files are automatically relocated, preserving the link to the cloud asset.

To commit tracking files, add the following path to your VCS:

```
uam/
```


## What happens when you move assets

When you move an imported asset within your Unity project (for example, from `Assets/Models/car.fbx` to `Assets/Vehicles/car.fbx`), the Asset Manager automatically:

1. Detects the move when Unity reloads the Asset Database.
2. Relocates the tracking file to match the new asset location. For example, from `uam/Models/car.fbx.json` to `uam/Vehicles/car.fbx.json`. The original cloud path (`path`) is preserved.

This happens transparently. The asset remains linked to the same cloud asset after the move.

> **Tip**:
> If you close Unity and move assets, the Asset Manager recovers tracking files automatically when you reopen Unity. During this process, the Console might display recovery messages.

## What happens to outdated assets

When a newer version of a cloud asset is available, the asset card in the Asset Manager window displays an **outdated** icon. For more information, refer to [Asset import states](import-assets.md#asset-import-states).

When an asset is outdated, you can reimport the asset to get the latest version. Refer to [Reimport an asset](import-assets.md#reimport-an-asset). After reimport, Asset Manager updates the tracking file with new version information and checksums.

If you reimport a locally modified asset, Asset Manager detects the modification by comparing the current file checksum against the stored checksum. This helps you decide whether to skip or overwrite local changes when reimporting.

## Migrating from versions before 1.10.0

If you upgrade Asset Manager for Unity from a version before 1.10.0, the migration to the new tracking file format is automatic. Consult the following sections on changes to the file format, information on the migration process, and the steps to follow after you upgrade.

### Tracking file changes

Asset Manager for Unity tracking information has changed significantly in 1.10.0. The following table describes tracking information and what has changed between versions. 

| Aspect | Version 1.9.1 and earlier | Version 1.10.0 and later                                                                                                  |
|--------|---------------------------|---------------------------------------------------------------------------------------------------------------------------|
| **File location** | `ProjectSettings/Packages/com.unity.asset-manager-for-unity/ImportedAssetInfo/` (flat structure) | `uam/<nested-directories>` (mirrors Assets folder) |
| **File naming** | One file per Asset Manager asset, named by asset ID. For example, `abc123def456ghi789`. | One file per Unity asset file, named by asset path with `.json` extension. For example, `Models/car.fbx.json`.                 |
| **File count** | One file regardless of how many Unity files the asset contains. | One file per Unity asset file.                                                                                             |

### Migration process

Migration occurs automatically the first time you open a project after upgrading. The migration process follows these steps: 

1. Asset Manager reads all existing tracking files (old format).
2. For each tracked asset, Asset Manager creates new tracking files in the tracking folder.
3. Asset Manager deletes old format files after successful migration.
4. The Console displays migration messages for tracked assets.

> **Note**:
> Legacy tracking files might reappear after you pull from version control. To run migration manually, select **Check for Tracking File Migration** from the Asset Manager context menu. 

### After migration

After migration completes, it's recommended that you do the following:

1. **Commit the changes**: Commit all changes in the tracking folder to your version control system (VCS). For the current path, refer to [Location](#location).
2. **Coordinate with your team**: If team members are using different package versions, ensure everyone upgrades before committing the migrated files. Older package versions can't read the new format.
3. **Verify tracking**: Open the **In Project** tab to confirm all previously imported assets are still tracked correctly.

> **Important**:
> Don't manually delete or modify tracking files during migration. Let Asset Manager handle the process automatically.

### If you have tracking files in the old location

Version 1.10.0 stores tracking files in `uam/` at the project root. If you have tracking files under the previous path (`ProjectSettings/Packages/com.unity.asset-manager-for-unity/ImportedAssetInfo/`), Asset Manager automatically migrates them to `uam/` when you open the project. 

If legacy tracking files appear after you pull from your VCS, select **Check for Tracking File Migration** from the Asset Manager window context menu to perform manual migration. When migration is complete, remove the previous path and commit the new `uam/` folder to your VCS.
