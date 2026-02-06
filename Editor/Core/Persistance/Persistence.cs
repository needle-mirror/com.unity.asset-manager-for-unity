using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    interface IPersistenceVersion
    {
        /// <summary>
        /// The major version number for this persistence implementation.
        /// Used to identify the primary schema or serialization format.
        /// </summary>
        int MajorVersion { get; }

        /// <summary>
        /// The minor version number for this persistence implementation.
        /// Used to distinguish feature or compatibility changes within the same major version.
        /// </summary>
        int MinorVersion { get; }

        /// <summary>
        /// Converts a tracking file's raw content (typically JSON or another serialized format)
        /// into an <see cref="ImportedAssetInfo"/> object according to this persistence version's schema.
        /// </summary>
        /// <param name="content">Serialized string content of the tracking file.</param>
        /// <returns>An <see cref="ImportedAssetInfo"/> object containing deserialized asset tracking data.</returns>
        ImportedAssetInfo ConvertToImportedAssetInfo(string content);

        /// <summary>
        /// Serializes an <see cref="AssetData"/> along with its associated file information
        /// into a string that represents the format of a tracking file for this version.
        /// This may include data for multiple Unity asset files (legacy/aggregate format).
        /// </summary>
        /// <param name="assetData">The asset metadata to persist.</param>
        /// <param name="fileInfos">The associated Unity asset file(s) to serialize.</param>
        /// <returns>
        /// A serialized string (e.g. JSON or other format) representing the tracking file content,
        /// or <c>null</c> if the entry cannot be serialized by this version.
        /// </returns>
        string SerializeEntry(AssetData assetData, IEnumerable<ImportedFileInfo> fileInfos);

        /// <summary>
        /// Serializes a single Unity asset file and associated <see cref="AssetData"/>
        /// into the "per-Unity-file" tracking file format for this version.
        /// Returns <c>null</c> if this persistence version does not support per-Unity-file serialization.
        /// </summary>
        /// <param name="assetData">The asset metadata to serialize.</param>
        /// <param name="fileInfo">The Unity asset file information to serialize.</param>
        /// <returns>
        /// A serialized string representing the tracking file content for the specific Unity asset file,
        /// or <c>null</c> if this persistence version does not support per-Unity-file serialization.
        /// </returns>
        string SerializeEntryForFile(AssetData assetData, ImportedFileInfo fileInfo);
    }

    /// <summary>
    /// Handles current persistence operations for tracking files.
    /// Migration logic is handled by PersistenceMigration.
    /// </summary>
    static class Persistence
    {
        static readonly string s_DefaultTrackedFolder =
            Path.Combine(Application.dataPath, "..", AssetManagerCoreConstants.TrackedFolderName);

        static readonly string s_LegacyTrackedFolder =
            Path.Combine(
                Application.dataPath,
                "..",
                "ProjectSettings",
                "Packages",
                AssetManagerCoreConstants.PackageName,
                "ImportedAssetInfo");

        static string s_OverrideTrackedFolder = null; // for testing purposes
        static string s_OverrideLegacyTrackedFolder = null; // for testing purposes
        internal static void OverrideTrackedFolder(string trackedFolder) => s_OverrideTrackedFolder = trackedFolder;
        internal static void OverrideLegacyTrackedFolder(string legacyTrackedFolder) => s_OverrideLegacyTrackedFolder = legacyTrackedFolder;

        internal static string TrackedFolder
        {
            get
            {
                var trackedFolder = s_DefaultTrackedFolder;

                if (!string.IsNullOrEmpty(s_OverrideTrackedFolder))
                {
                    trackedFolder = s_OverrideTrackedFolder;
                }

                return trackedFolder;
            }
        }

        internal static string LegacyTrackedFolder =>
            !string.IsNullOrEmpty(s_OverrideLegacyTrackedFolder) ? s_OverrideLegacyTrackedFolder : s_LegacyTrackedFolder;

        const string k_TrackingFolderReadmeFileName = "README.txt";

        /// <summary>
        /// Ensures the tracked folder exists and contains README.txt (creates both if missing).
        /// Call before writing tracking files and before starting the file watcher so the folder is never created without the readme.
        /// </summary>
        internal static void EnsureTrackedFolderAndReadme(IIOProxy ioProxy)
        {
            if (ioProxy == null || string.IsNullOrEmpty(TrackedFolder))
                return;
            EnsureDirectoryExistsAndReadmeIfNew(ioProxy, TrackedFolder);
        }

        /// <summary>
        /// Ensures the directory exists and adds README.txt only when the directory is created for the first time.
        /// Used by WriteEntry, by location migration, and when starting the tracked-folder watcher.
        /// </summary>
        internal static void EnsureDirectoryExistsAndReadmeIfNew(IIOProxy ioProxy, string folderPath)
        {
            if (ioProxy == null || string.IsNullOrEmpty(folderPath))
                return;
            var folderExisted = ioProxy.DirectoryExists(folderPath);
            ioProxy.EnsureDirectoryExists(folderPath);
            if (!folderExisted)
                WriteReadmeToTrackedFolder(ioProxy, folderPath);
        }

        /// <summary>
        /// Writes README.txt into the given tracking folder. Only call when the directory has just been created for the first time.
        /// </summary>
        static void WriteReadmeToTrackedFolder(IIOProxy ioProxy, string trackedFolderPath)
        {
            if (ioProxy == null || string.IsNullOrEmpty(trackedFolderPath))
                return;
            try
            {
                ioProxy.FileWriteAllText(Path.Combine(trackedFolderPath, k_TrackingFolderReadmeFileName), GetTrackingFolderReadmeContent());
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not write {k_TrackingFolderReadmeFileName} to tracking folder: {e.Message}");
            }
        }

        static string GetTrackingFolderReadmeContent()
        {
            return "This folder is auto-generated by the com.unity.asset-manager-for-unity package (version 1.10 and later).\n\n"
                + "The files in this folder are created automatically when you track assets from Asset Manager.\n\n"
                + "You should commit this folder to source control so your team shares the same tracking state.\n\n"
                + "Documentation: " + PackageDocumentation.GetPackageManualPageUrl("tracking-files") + "\n\n"
                + "This README can be deleted.";
        }

        internal class ReadCache
        {
            public Dictionary<AssetIdentifier, AssetData> s_AssetDatas = new(); // assetId => AssetData

            public AssetData GetAssetDataFor(AssetIdentifier assetIdentifier)
            {
                if (!s_AssetDatas.TryGetValue(assetIdentifier, out var assetData))
                {
                    assetData = new AssetData();
                    s_AssetDatas[assetIdentifier] = assetData;
                }
                return assetData;
            }
        }

        public static IReadOnlyCollection<ImportedAssetInfo> ReadAllEntries(IIOProxy ioProxy, out MigrationResult migrationResult, Dictionary<string, string> trackingPathToAssetId = null)
        {
            migrationResult = default;

            if (ioProxy == null)
            {
                Utilities.DevLogError("Null IIOProxy service");
                return Array.Empty<ImportedAssetInfo>();
            }

            return PersistenceMigration.ReadAllEntries(ioProxy, TrackedFolder, LegacyTrackedFolder, out migrationResult, trackingPathToAssetId);
        }

        /// <param name="migrationResult">Indicates whether the file was migrated and success/failure.</param>
        public static ImportedAssetInfo ReadEntry(IIOProxy ioProxy, string filePath, out MigrationResult migrationResult)
        {
            migrationResult = default;

            if (ioProxy == null)
            {
                Utilities.DevLogError("Null IIOProxy service");
                return null;
            }

            // Delegate to migration layer for reading single entry
            return PersistenceMigration.ReadEntry(ioProxy, filePath, out migrationResult);
        }

        /// <summary>
        /// Moves a tracking file from the old Unity asset path to the new path.
        /// Updates the path in the tracking file and moves the file to the new location.
        /// Uses GUID-based search as fallback if the file is not found at the old path.
        /// </summary>
        public static void MoveTrackingFile(IIOProxy ioProxy, IAssetDatabaseProxy assetDatabaseProxy, string oldUnityAssetPath, string newUnityAssetPath)
        {
            if (ioProxy == null || assetDatabaseProxy == null || string.IsNullOrEmpty(oldUnityAssetPath) || string.IsNullOrEmpty(newUnityAssetPath))
            {
                return;
            }

            var oldTrackingFilePath = GetTrackingFilePathForUnityAsset(oldUnityAssetPath);
            var newTrackingFilePath = GetTrackingFilePathForUnityAsset(newUnityAssetPath);

            if (string.IsNullOrEmpty(oldTrackingFilePath) || string.IsNullOrEmpty(newTrackingFilePath))
            {
                return;
            }

            // Normalize paths
            oldTrackingFilePath = Path.GetFullPath(oldTrackingFilePath);
            newTrackingFilePath = Path.GetFullPath(newTrackingFilePath);

            // If paths are the same (after normalization), no move needed
            if (oldTrackingFilePath == newTrackingFilePath)
            {
                return;
            }

            if (!ioProxy.FileExists(oldTrackingFilePath))
            {
                // File doesn't exist - might have been moved externally, try GUID-based search
                Utilities.DevLog($"Tracking file not found at old path: {oldTrackingFilePath}. Attempting GUID-based search.", DevLogHighlightColor.Yellow);

                // Get GUID from new path and search for tracking file with that GUID
                var guid = assetDatabaseProxy.AssetPathToGuid(newUnityAssetPath);
                if (!string.IsNullOrEmpty(guid))
                {
                    var foundTrackingFile = FindTrackingFileByGuid(ioProxy, guid);
                    if (!string.IsNullOrEmpty(foundTrackingFile) && foundTrackingFile != newTrackingFilePath)
                    {
                        // Found tracking file at different location - move it to new path
                        oldTrackingFilePath = foundTrackingFile;
                        Utilities.DevLog($"Found tracking file by GUID at: {foundTrackingFile}. Moving to new path.", highlight: true);
                    }
                    else if (string.IsNullOrEmpty(foundTrackingFile))
                    {
                        // No tracking file found - might be a new asset or not tracked
                        Utilities.DevLog($"No tracking file found for GUID {guid}. Asset may not be tracked.");
                        return;
                    }
                    else
                    {
                        // Tracking file already at correct location - no action needed.
                        return;
                    }
                }
                else
                {
                    Utilities.DevLog($"Could not get GUID for asset path: {newUnityAssetPath}", DevLogHighlightColor.Yellow);
                    return;
                }
            }

            try
            {
                // Read the existing tracking file
                var content = ioProxy.FileReadAllText(oldTrackingFilePath);
                if (string.IsNullOrEmpty(content))
                {
                    return;
                }

                var currentVersion = PersistenceMigration.GetCurrentVersion();
                var importedAssetInfo = currentVersion.ConvertToImportedAssetInfo(content);
                if (importedAssetInfo == null || importedAssetInfo.FileInfos == null || !importedAssetInfo.FileInfos.Any())
                {
                    return;
                }

                if (!(importedAssetInfo.AssetData is AssetData assetData))
                {
                    Utilities.DevLogError($"Cannot move tracking file: AssetData is not of expected type");
                    return;
                }

                // Ensure every FileInfo (other than the one we're moving) has a tracking file at its path
                // before we delete the original, so no file infos are left without persistence.
                foreach (var fi in importedAssetInfo.FileInfos)
                {
                    if (fi == null || string.IsNullOrEmpty(fi.OriginalPath))
                        continue;
                    if (PathsReferToSameUnityAsset(fi.OriginalPath, oldUnityAssetPath))
                        continue; // This one we're moving; handle below

                    var trackingPath = GetTrackingFilePathForUnityAsset(fi.OriginalPath);
                    if (string.IsNullOrEmpty(trackingPath))
                        continue;
                    var normalizedTrackingPath = Path.GetFullPath(trackingPath);
                    if (normalizedTrackingPath == oldTrackingFilePath)
                        continue; // Same as the file we're deleting; will be replaced by the moved file or another write

                    if (!ioProxy.FileExists(normalizedTrackingPath))
                    {
                        var fileContent = currentVersion.SerializeEntryForFile(assetData, fi);
                        if (!string.IsNullOrEmpty(fileContent))
                        {
                            var dir = Path.GetDirectoryName(normalizedTrackingPath);
                            if (!string.IsNullOrEmpty(dir))
                                ioProxy.EnsureDirectoryExists(dir);
                            WriteFile(ioProxy, normalizedTrackingPath, fileContent);
                        }
                    }
                }

                // Find the file info we're moving (match by path; V4 has one file per tracking file so .First() is fallback)
                var fileInfoToMove = importedAssetInfo.FileInfos.FirstOrDefault(fi =>
                    fi != null && PathsReferToSameUnityAsset(fi.OriginalPath, oldUnityAssetPath));
                if (fileInfoToMove == null)
                    fileInfoToMove = importedAssetInfo.FileInfos.First();

                var updatedContent = currentVersion.SerializeEntryForFile(assetData, fileInfoToMove);
                if (string.IsNullOrEmpty(updatedContent))
                {
                    return;
                }

                var newDirectory = Path.GetDirectoryName(newTrackingFilePath);
                if (!string.IsNullOrEmpty(newDirectory))
                {
                    ioProxy.EnsureDirectoryExists(newDirectory);
                }

                WriteFile(ioProxy, newTrackingFilePath, updatedContent);

                if (ioProxy.FileExists(oldTrackingFilePath))
                {
                    ioProxy.DeleteFile(oldTrackingFilePath);
                }

                Utilities.DevLog($"Moved tracking file from {oldUnityAssetPath} to {newUnityAssetPath}", highlight: true);
            }
            catch (Exception e)
            {
                Utilities.DevLogError($"Failed to move tracking file from {oldUnityAssetPath} to {newUnityAssetPath}: {e.Message}");
                Utilities.DevLogException(e);
            }
        }

        /// <summary>
        /// Finds a tracking file by searching for one that contains the specified Unity GUID.
        /// Returns the full path to the tracking file, or null if not found.
        /// </summary>
        static string FindTrackingFileByGuid(IIOProxy ioProxy, string guid)
        {
            if (string.IsNullOrEmpty(guid) || !ioProxy.DirectoryExists(TrackedFolder))
            {
                return null;
            }

            try
            {
                // Search all .json files in the tracked folder
                var trackingFiles = ioProxy.EnumerateFiles(TrackedFolder, "*.json", SearchOption.AllDirectories);
                var currentVersion = PersistenceMigration.GetCurrentVersion();

                foreach (var trackingFile in trackingFiles)
                {
                    try
                    {
                        var content = ioProxy.FileReadAllText(trackingFile);
                        if (string.IsNullOrEmpty(content) || !content.Contains(guid))
                            continue;

                        var importedAssetInfo = currentVersion.ConvertToImportedAssetInfo(content);
                        if (importedAssetInfo?.FileInfos != null)
                        {
                            foreach (var fileInfo in importedAssetInfo.FileInfos)
                            {
                                if (fileInfo.Guid == guid)
                                {
                                    return Path.GetFullPath(trackingFile);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip files that can't be parsed
                        continue;
                    }
                }
            }
            catch (Exception e)
            {
                Utilities.DevLogError($"Failed to search for tracking file by GUID: {e.Message}");
                Utilities.DevLogException(e);
            }

            return null;
        }

        static bool s_DirectoryCleanupScheduled;
        static IIOProxy s_ScheduledCleanupIOProxy;

        /// <summary>
        /// Schedules removal of empty subdirectories under <see cref="TrackedFolder"/>.
        /// Multiple calls within the same editor frame are deduplicated so the scan runs only once.
        /// The actual cleanup is deferred via <see cref="EditorApplication.delayCall"/> to batch
        /// multiple requests and avoid redundant filesystem operations.
        ///
        /// Enumerates deepest-first so nested empty directories are cleaned in a single pass.
        /// Never deletes the tracked folder root itself. Safe to call at any time -- only
        /// operates within the known tracked folder constant.
        /// </summary>
        internal static void CleanEmptySubdirectories(IIOProxy ioProxy)
        {
            if (ioProxy == null)
                return;

            if (s_DirectoryCleanupScheduled)
                return;

            s_DirectoryCleanupScheduled = true;
            s_ScheduledCleanupIOProxy = ioProxy;
            EditorApplication.delayCall += RunScheduledDirectoryCleanup;
        }

        static void RunScheduledDirectoryCleanup()
        {
            var ioProxy = s_ScheduledCleanupIOProxy;
            s_ScheduledCleanupIOProxy = null;
            s_DirectoryCleanupScheduled = false;

            if (ioProxy == null)
                return;

            CleanEmptySubdirectoriesInternal(ioProxy);
        }

        /// <summary>
        /// Internal implementation that performs the actual directory cleanup.
        /// Called by the scheduled cleanup after deduplication.
        /// </summary>
        static void CleanEmptySubdirectoriesInternal(IIOProxy ioProxy)
        {
            if (ioProxy == null || string.IsNullOrEmpty(TrackedFolder) || !ioProxy.DirectoryExists(TrackedFolder))
                return;

            // Critical safety validation: ensure we're operating on the correct folder (skip when override is set for tests)
            if (string.IsNullOrEmpty(s_OverrideTrackedFolder) && !IsValidTrackedFolderPath(TrackedFolder))
            {
                Utilities.DevLogError($"CleanEmptySubdirectories: Refusing to operate on invalid tracked folder path: {TrackedFolder}");
                return;
            }

            // Get all subdirectories, sorted deepest first so children are removed before parents.
            var subdirectories = ioProxy.GetDirectories(TrackedFolder, "*", SearchOption.AllDirectories);
            Array.Sort(subdirectories, (a, b) => b.Length.CompareTo(a.Length));

            foreach (var dir in subdirectories)
            {
                try
                {
                    if (!ioProxy.DirectoryExists(dir))
                        continue;

                    // Additional safety check: ensure the directory is still within our tracked folder
                    if (!IsSubdirectoryOfTrackedFolder(dir))
                    {
                        Utilities.DevLogError($"CleanEmptySubdirectories: Refusing to delete directory outside tracked folder: {dir}");
                        continue;
                    }

                    if (!ioProxy.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Any())
                        ioProxy.DirectoryDelete(dir, false);
                }
                catch (Exception e)
                {
                    Utilities.DevLogError($"CleanEmptySubdirectories: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Validates that the tracked folder path is safe for deletion operations.
        /// Performs multiple safety checks:
        /// 1. The folder must be named exactly "uam" (from TrackedFolderName constant)
        /// 2. The folder must be at the same level as the Assets folder (sibling to Assets)
        /// 3. The folder must be within the current project root
        /// 4. The path must not match known dangerous paths
        /// </summary>
        /// <returns>True if the path is valid and safe for operations, false otherwise.</returns>
        internal static bool IsValidTrackedFolderPath(string trackedFolderPath)
        {
            if (string.IsNullOrEmpty(trackedFolderPath))
                return false;

            try
            {
                // Normalize the path for reliable comparison
                var normalizedTrackedPath = Path.GetFullPath(trackedFolderPath);

                // 1. Check that the folder is named exactly as expected
                var folderName = Path.GetFileName(normalizedTrackedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

#if UNITY_EDITOR_WIN
                var comparisonType = StringComparison.OrdinalIgnoreCase;
#else
                var comparisonType = StringComparison.Ordinal;
#endif

                if (!string.Equals(folderName, AssetManagerCoreConstants.TrackedFolderName, comparisonType))
                {
                    Utilities.DevLogError($"IsValidTrackedFolderPath: Folder name '{folderName}' does not match expected '{AssetManagerCoreConstants.TrackedFolderName}'");
                    return false;
                }

                // 2. Get the project root (parent of Assets folder)
                // Application.dataPath returns path to Assets folder when running in Editor
                var assetsPath = Path.GetFullPath(Application.dataPath);
                if (!assetsPath.EndsWith(AssetManagerCoreConstants.AssetsFolderName, comparisonType))
                {
                    Utilities.DevLogError($"IsValidTrackedFolderPath: Application.dataPath '{assetsPath}' does not end with expected '{AssetManagerCoreConstants.AssetsFolderName}'");
                    return false;
                }

                var projectRoot = Path.GetDirectoryName(assetsPath);

                if (string.IsNullOrEmpty(projectRoot))
                {
                    Utilities.DevLogError("IsValidTrackedFolderPath: Could not determine project root");
                    return false;
                }

                // 3. Verify the tracked folder is at the same level as Assets (sibling to Assets)
                var trackedFolderParent = Path.GetDirectoryName(normalizedTrackedPath);

                if (!string.Equals(trackedFolderParent, projectRoot, comparisonType))
                {
                    Utilities.DevLogError($"IsValidTrackedFolderPath: Tracked folder parent '{trackedFolderParent}' is not the project root '{projectRoot}'");
                    return false;
                }

                // 4. Build the expected full path and verify it matches
                var expectedFullPath = Path.GetFullPath(Path.Combine(projectRoot, AssetManagerCoreConstants.TrackedFolderName));

                if (!string.Equals(normalizedTrackedPath, expectedFullPath, comparisonType))
                {
                    Utilities.DevLogError($"IsValidTrackedFolderPath: Path '{normalizedTrackedPath}' does not match expected '{expectedFullPath}'");
                    return false;
                }

                // 5. Safety check: ensure we're not pointing to dangerous paths
                if (IsDangerousPath(normalizedTrackedPath))
                {
                    Utilities.DevLogError($"IsValidTrackedFolderPath: Path '{normalizedTrackedPath}' matches a known dangerous path pattern");
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Utilities.DevLogError($"IsValidTrackedFolderPath: Exception during validation: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a path matches known dangerous path patterns that should never be deleted.
        /// </summary>
        static bool IsDangerousPath(string normalizedPath)
        {
            if (string.IsNullOrEmpty(normalizedPath))
                return true;

            // Normalize for comparison
            var pathLower = normalizedPath.ToLowerInvariant();
            var trimmedPath = pathLower.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Known dangerous paths (system folders, user folders, etc.)
            var dangerousPaths = new[]
            {
                // Unix/macOS dangerous paths
                "/",
                "/bin",
                "/etc",
                "/home",
                "/lib",
                "/opt",
                "/root",
                "/sbin",
                "/tmp",
                "/usr",
                "/var",
                "/applications",
                "/system",
                "/library",

                // User home directories (macOS/Linux)
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).ToLowerInvariant(),
                Environment.GetFolderPath(Environment.SpecialFolder.Personal).ToLowerInvariant(),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop).ToLowerInvariant(),

                // Windows dangerous paths
                "c:\\",
                "c:\\windows",
                "c:\\program files",
                "c:\\program files (x86)",
                "c:\\users",
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles).ToLowerInvariant(),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86).ToLowerInvariant(),
                Environment.GetFolderPath(Environment.SpecialFolder.System).ToLowerInvariant(),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows).ToLowerInvariant(),
            };

            foreach (var dangerousPath in dangerousPaths)
            {
                if (string.IsNullOrEmpty(dangerousPath))
                    continue;

                var normalizedDangerous = dangerousPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(trimmedPath, normalizedDangerous, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Verifies that a directory path is a subdirectory of the tracked folder.
        /// Used as an additional safety check before deletion.
        /// </summary>
        static bool IsSubdirectoryOfTrackedFolder(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath) || string.IsNullOrEmpty(TrackedFolder))
                return false;

            try
            {
                var normalizedDir = Path.GetFullPath(directoryPath);
                var normalizedTracked = Path.GetFullPath(TrackedFolder);

#if UNITY_EDITOR_WIN
                var comparisonType = StringComparison.OrdinalIgnoreCase;
#else
                var comparisonType = StringComparison.Ordinal;
#endif

                // Ensure the directory starts with the tracked folder path
                // Add separator to prevent partial matches (e.g., "uam" matching "uam-other")
                var trackedWithSeparator = normalizedTracked.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                return normalizedDir.StartsWith(trackedWithSeparator, comparisonType);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Removes all tracking files for the given Asset Manager asset.
        /// </summary>
        public static void RemoveEntry(IIOProxy ioProxy, string assetId)
        {
            if (ioProxy == null || string.IsNullOrEmpty(assetId))
            {
                return;
            }

            if (!ioProxy.DirectoryExists(TrackedFolder))
            {
                return;
            }

            var allEntries = PersistenceMigration.ReadAllEntries(ioProxy, TrackedFolder, LegacyTrackedFolder, out _);
            foreach (var entry in allEntries)
            {
                if (entry?.Identifier?.AssetId == assetId)
                {
                    foreach (var fileInfo in entry.FileInfos)
                    {
                        if (fileInfo != null && !string.IsNullOrEmpty(fileInfo.OriginalPath))
                        {
                            var trackingFilePath = GetTrackingFilePathForUnityAsset(fileInfo.OriginalPath);
                            if (!string.IsNullOrEmpty(trackingFilePath) && ioProxy.FileExists(trackingFilePath))
                                ioProxy.DeleteFile(trackingFilePath);
                        }
                    }
                }
            }

            CleanEmptySubdirectories(ioProxy);
        }

        /// <summary>
        /// Writes one tracking file per Unity asset.
        /// Continues processing after path-too-long errors to write as many files as possible.
        /// </summary>
        /// <exception cref="TrackingFilePathTooLongException">
        /// Thrown after all files are processed if any tracking file paths exceeded the OS maximum path length.
        /// The exception contains both the failed paths and the successfully written file infos.
        /// </exception>
        public static void WriteEntry(IIOProxy ioProxy, AssetData assetData, IEnumerable<ImportedFileInfo> fileInfos)
        {
            if (assetData == null || fileInfos == null)
            {
                return;
            }

            EnsureTrackedFolderAndReadme(ioProxy);
            var currentVersion = PersistenceMigration.GetCurrentVersion();
            var failedPaths = new List<string>();
            var successfulFileInfos = new List<ImportedFileInfo>();
            Exception firstException = null;

            foreach (var fileInfo in fileInfos)
            {
                if (fileInfo == null || string.IsNullOrEmpty(fileInfo.Guid))
                {
                    continue;
                }

                var fileContent = currentVersion.SerializeEntryForFile(assetData, fileInfo);
                if (string.IsNullOrEmpty(fileContent))
                {
                    continue;
                }

                var trackingFilePath = GetTrackingFilePathForUnityAsset(fileInfo.OriginalPath);
                if (string.IsNullOrEmpty(trackingFilePath))
                {
                    continue;
                }

                try
                {
                    WriteFile(ioProxy, trackingFilePath, fileContent);
                    successfulFileInfos.Add(fileInfo);
                }
                catch (PathTooLongException e)
                {
                    Debug.LogError($"Path too long for tracking file: {trackingFilePath} :\n{e.Message}");
                    failedPaths.Add(fileInfo.OriginalPath);
                    firstException ??= e;
                }
            }

            // If any files failed, throw an exception with complete information
            if (failedPaths.Count > 0)
            {
                throw new TrackingFilePathTooLongException(failedPaths, successfulFileInfos, firstException);
            }
        }

        /// <summary>
        /// Converts a Unity asset path to a tracking file path.
        /// Example: "Assets/Cars/Models/delivery.fbx" -> "uam/Cars/Models/delivery.fbx.json"
        /// Unity asset paths are already validated by Unity, so we can use them directly.
        /// </summary>
        internal static string GetTrackingFilePathForUnityAsset(string unityAssetPath)
        {
            if (string.IsNullOrEmpty(unityAssetPath))
            {
                return null;
            }

            // Remove "Assets/" prefix if present
            var relativePath = unityAssetPath;
            if (unityAssetPath.StartsWith(AssetManagerCoreConstants.AssetsFolderName + Path.DirectorySeparatorChar) ||
                unityAssetPath.StartsWith(AssetManagerCoreConstants.AssetsFolderName + Path.AltDirectorySeparatorChar))
            {
                relativePath = unityAssetPath.Substring(AssetManagerCoreConstants.AssetsFolderName.Length + 1);
            }
            else if (unityAssetPath.StartsWith(AssetManagerCoreConstants.AssetsFolderName))
            {
                relativePath = unityAssetPath.Substring(AssetManagerCoreConstants.AssetsFolderName.Length);
            }

            // Normalize path separators so Path.Combine produces a consistent path (avoids mixed \ and / on Windows)
            var normalizedRelative = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            // Append .json extension
            var trackingFileName = normalizedRelative + ".json";

            return Path.Combine(TrackedFolder, trackingFileName);
        }

        /// <summary>
        /// Returns true if both paths refer to the same Unity asset (normalizes slashes for comparison).
        /// </summary>
        static bool PathsReferToSameUnityAsset(string path1, string path2)
        {
            if (string.IsNullOrEmpty(path1) || string.IsNullOrEmpty(path2))
                return false;
            var normalized1 = path1.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            var normalized2 = path2.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return string.Equals(normalized1, normalized2, StringComparison.OrdinalIgnoreCase);
        }

        static void WriteEntry(IIOProxy ioProxy, string assetId, string fileContent)
        {
            var importInfoFilePath = GetFilenameFor(assetId);
            WriteFile(ioProxy, importInfoFilePath, fileContent);
        }

        /// <summary>
        /// Writes content to a file path.
        /// </summary>
        /// <exception cref="PathTooLongException">Thrown when the path exceeds the operating system's maximum path length.</exception>
        static void WriteFile(IIOProxy ioProxy, string filePath, string fileContent)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

#if UNITY_EDITOR_WIN
            // Proactively check path length on Windows. Some Windows configurations enable
            // long path support, which prevents the OS from throwing PathTooLongException
            // even for paths exceeding MAX_PATH (260). We enforce this limit explicitly
            // to ensure consistent behavior across all Windows configurations.
            const int k_WindowsMaxPath = 260;
            if (filePath.Length >= k_WindowsMaxPath)
            {
                throw new PathTooLongException(
                    $"The specified path ({filePath.Length} characters) exceeds the Windows maximum path length of {k_WindowsMaxPath} characters: {filePath}");
            }
#endif

            try
            {
                var directoryName = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryName))
                {
                    ioProxy.EnsureDirectoryExists(directoryName);
                }
                ioProxy.FileWriteAllText(filePath, fileContent);
            }
            catch (PathTooLongException)
            {
                // Let propagate so caller can wrap with affected Unity asset path
                throw;
            }
            catch (IOException e)
            {
                Debug.Log($"Couldn't write imported asset info to {filePath} :\n{e}.");
            }
            catch (ArgumentException e)
            {
                // Path operations can throw ArgumentException for invalid characters
                // This should not happen with Unity-validated paths, but handle gracefully
                Debug.LogError($"Invalid path for imported asset info: {filePath} :\n{e}.");
            }
        }

        static string GetFilenameFor(string assetId)
        {
            return Path.Combine(TrackedFolder, assetId);
        }

        /// <summary>
        /// Extracts the Asset Manager assetId from a tracking file path.
        /// For legacy formats, the filename is the assetId.
        /// For per-Unity-file format, reads the file to get the assetId.
        /// </summary>
        internal static string ExtractAssetIdFromFilePath(IIOProxy ioProxy, string filePath)
        {
            if (ioProxy == null || string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            // If file doesn't exist, skip reading and go straight to path-based extraction
            // This is important for OnFileRemoved where the file is already deleted
            if (ioProxy.FileExists(filePath))
            {
                // Try to read the file first to get the assetId (works for both formats)
                var importedAssetInfo = ReadEntry(ioProxy, filePath, out _);
                if (importedAssetInfo?.Identifier?.AssetId != null)
                {
                    return importedAssetInfo.Identifier.AssetId;
                }
            }

            // If reading failed or file doesn't exist, try to determine format from path
            // Legacy format: filename is assetId (no .json extension, in top-level directory)
            try
            {
                var relativePath = Path.GetRelativePath(TrackedFolder, filePath);
                if (!relativePath.Contains(Path.DirectorySeparatorChar) &&
                    !relativePath.Contains(Path.AltDirectorySeparatorChar) &&
                    !relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    // Legacy format: filename is assetId
                    return Path.GetFileName(filePath);
                }
            }
            catch (ArgumentException)
            {
                // Path.GetRelativePath can throw if paths are not related
                // Fall through to return null
            }

            // Per-Unity-file format: can't determine assetId without reading the file
            // Return null to indicate we couldn't extract it
            return null;
        }
    }

    /// <summary>
    /// Thrown when one or more tracking files cannot be written because the path exceeds the operating system's maximum path length.
    /// Contains information about both the failed paths and the successfully written file infos.
    /// </summary>
    class TrackingFilePathTooLongException : Exception
    {
        /// <summary>
        /// The Unity asset paths that could not be written (e.g. "Assets/SomeFolder/asset.fbx").
        /// </summary>
        public IReadOnlyList<string> AffectedPaths { get; }

        /// <summary>
        /// The file infos that were successfully written before/despite the failures.
        /// </summary>
        public IReadOnlyList<ImportedFileInfo> SuccessfullyWrittenFileInfos { get; }

        /// <summary>
        /// Gets the first affected path for backward compatibility with error messages.
        /// </summary>
        public string AffectedPath => AffectedPaths.Count > 0 ? AffectedPaths[0] : string.Empty;

        public TrackingFilePathTooLongException(
            IReadOnlyList<string> affectedPaths,
            IReadOnlyList<ImportedFileInfo> successfullyWrittenFileInfos,
            Exception innerException)
            : base($"Path too long for tracking file(s): {string.Join(", ", affectedPaths)}", innerException)
        {
            AffectedPaths = affectedPaths ?? Array.Empty<string>();
            SuccessfullyWrittenFileInfos = successfullyWrittenFileInfos ?? Array.Empty<ImportedFileInfo>();
        }
    }
}
