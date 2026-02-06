using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Categorizes why a single tracking file migration failed.
    /// </summary>
    enum MigrationFailureReason
    {
        VersionUpgradeFailed,
        DeserializationFailed,
        WriteFailed,
        ReadFailed
    }

    /// <summary>
    /// Result of a tracking file migration pass: whether any migration occurred and counts of successes and failures by reason.
    /// </summary>
    struct MigrationResult
    {
        public bool MigrationOccurred;
        public int SuccessCount;
        public int FailureCount;
        public int VersionUpgradeFailedCount;
        public int DeserializationFailedCount;
        public int WriteFailedCount;
        public int ReadFailedCount;

        public void AddFailure(MigrationFailureReason reason)
        {
            FailureCount++;
            switch (reason)
            {
                case MigrationFailureReason.VersionUpgradeFailed:
                    VersionUpgradeFailedCount++;
                    break;
                case MigrationFailureReason.DeserializationFailed:
                    DeserializationFailedCount++;
                    break;
                case MigrationFailureReason.WriteFailed:
                    WriteFailedCount++;
                    break;
                case MigrationFailureReason.ReadFailed:
                    ReadFailedCount++;
                    break;
            }
        }
    }

    /// <summary>
    /// Handles migration between different persistence format versions.
    /// Separates migration concerns from current persistence operations.
    /// </summary>
    static class PersistenceMigration
    {
        const string k_FileSearchPattern = "*.json";

        static readonly Regex s_SerializationVersionRegex =
            new("\"serializationVersion\"\\s*:\\s*\\[\\s*(\\d+)\\s*,\\s*(\\d+)\\s*\\]",
                RegexOptions.None,
                TimeSpan.FromMilliseconds(100));

        static readonly IPersistenceVersion[] s_PersistenceVersions =
        {
            new PersistenceLegacy(),
            new PersistenceV1(),
            new PersistenceV2(),
            new PersistenceV3(),
            new PersistenceV4() // First iteration of per-Unity-file tracking
        };

        /// <summary>
        /// Gets the current (latest) persistence version.
        /// </summary>
        public static IPersistenceVersion GetCurrentVersion()
        {
            return s_PersistenceVersions[^1];
        }

        /// <summary>
        /// Moves all tracking files from the legacy folder to the current tracking folder, preserving relative path structure.
        /// Then deletes the legacy folder tree.
        /// </summary>
        static void MigrateLegacyFolderToNewLocation(IIOProxy ioProxy, string legacyTrackedFolder, string trackedFolder)
        {
            Persistence.EnsureDirectoryExistsAndReadmeIfNew(ioProxy, trackedFolder);

            var legacyFiles = new List<string>();
            legacyFiles.AddRange(ioProxy.EnumerateFiles(legacyTrackedFolder, k_FileSearchPattern, SearchOption.AllDirectories));
            foreach (var file in ioProxy.EnumerateFiles(legacyTrackedFolder, "*", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(file);
                if (string.IsNullOrEmpty(Path.GetExtension(file)) && !fileName.StartsWith(".", StringComparison.Ordinal))
                {
                    legacyFiles.Add(file);
                }
            }

            foreach (var legacyFilePath in legacyFiles)
            {
                try
                {
                    var relativePath = Path.GetRelativePath(legacyTrackedFolder, legacyFilePath);
                    var destPath = Path.Combine(trackedFolder, relativePath);
                    var destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir))
                    {
                        ioProxy.EnsureDirectoryExists(destDir);
                    }
                    ioProxy.FileMove(legacyFilePath, destPath);
                    Utilities.DevLog($"Migrated tracking file to new location: {relativePath}", DevLogHighlightColor.Cyan);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to migrate tracking file from '{legacyFilePath}' to new location: {e.Message}");
                    Utilities.DevLogException(e);
                }
            }

            try
            {
                if (ioProxy.DirectoryExists(legacyTrackedFolder))
                {
                    // Only delete if no files remain (i.e. all migrations were successful)
                    bool isLegacyFolderEmpty = !ioProxy.EnumerateFiles(legacyTrackedFolder, "*", SearchOption.AllDirectories).Any();
                    if (isLegacyFolderEmpty)
                    {
                        ioProxy.DirectoryDelete(legacyTrackedFolder, true);
                        Utilities.DevLog("Removed legacy tracking folder after migration.", DevLogHighlightColor.Yellow);
                    }
                    else
                    {
                        Debug.LogWarning($"Legacy tracking folder '{legacyTrackedFolder}' was not deleted because it still contains files (migration may have failed for some files).");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to delete legacy tracking folder '{legacyTrackedFolder}': {e.Message}");
            }
        }

        /// <summary>
        /// Reads all tracking files, handling both legacy formats and current format.
        /// If legacy files are detected they will be migrated automatically.
        /// If <paramref name="legacyTrackedFolder"/> exists, files there are moved to <paramref name="trackedFolder"/> (on-the-fly location migration).
        /// </summary>
        /// <param name="migrationResult">Indicates whether any migration occurred and the count of successfully migrated files and failures.</param>
        /// <param name="trackingPathToAssetId">Optional. If non-null, populated with (physical tracking file path, assetId) for each read file.</param>
        public static IReadOnlyCollection<ImportedAssetInfo> ReadAllEntries(IIOProxy ioProxy, string trackedFolder, string legacyTrackedFolder, out MigrationResult migrationResult, Dictionary<string, string> trackingPathToAssetId = null)
        {
            migrationResult = default;

            if (ioProxy == null)
            {
                return Array.Empty<ImportedAssetInfo>();
            }

            // On-the-fly migration: move files from old directory (ProjectSettings/.../ImportedAssetInfo) to new directory (uam)
            var normalizedLegacy = !string.IsNullOrEmpty(legacyTrackedFolder) ? Path.GetFullPath(legacyTrackedFolder) : null;
            var normalizedTracked = Path.GetFullPath(trackedFolder);
            if (!string.IsNullOrEmpty(normalizedLegacy) &&
                normalizedLegacy != normalizedTracked &&
                ioProxy.DirectoryExists(legacyTrackedFolder))
            {
                MigrateLegacyFolderToNewLocation(ioProxy, normalizedLegacy, normalizedTracked);
                migrationResult.MigrationOccurred = true;
            }

            if (!ioProxy.DirectoryExists(trackedFolder))
            {
                return Array.Empty<ImportedAssetInfo>();
            }

            var currentVersionNumber = GetMaxSupportedVersion();
            var partialInfos = new List<ImportedAssetInfo>();

            // Collect all files: *.json in all directories + extensionless files in top-level (legacy format)
            var filesToProcess = new List<string>();
            filesToProcess.AddRange(ioProxy.EnumerateFiles(trackedFolder, k_FileSearchPattern, SearchOption.AllDirectories));
            foreach (var file in ioProxy.EnumerateFiles(trackedFolder, "*", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(file);
                if (string.IsNullOrEmpty(Path.GetExtension(file)) && !fileName.StartsWith(".", StringComparison.Ordinal))
                {
                    filesToProcess.Add(file);
                }
            }

            foreach (var assetPath in filesToProcess)
            {
                try
                {
                    var content = ioProxy.FileReadAllText(assetPath);

                    // Skip empty files
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        continue;
                    }

                    var (major, minor) = ExtractSerializationVersion(content);

                    if (major > currentVersionNumber)
                    {
                        Debug.LogError(
                            $"Unsupported serialization version {major}.{minor} in tracking file '{assetPath}'");
                        continue;
                    }

                    ImportedAssetInfo importedAssetInfo;
                    if (major == currentVersionNumber)
                    {
                        // For current version files, use content directly (no migration needed)
                        importedAssetInfo = s_PersistenceVersions[major].ConvertToImportedAssetInfo(content);
                    }
                    else
                    {
                        // For older versions, migrate to latest, write new files, and delete legacy file
                        importedAssetInfo = MigrateAndWriteEntry(ioProxy, assetPath, content, out var failureReason);
                        if (importedAssetInfo != null)
                        {
                            migrationResult.MigrationOccurred = true;
                            migrationResult.SuccessCount++;
                        }
                        else
                        {
                            migrationResult.AddFailure(failureReason ?? MigrationFailureReason.DeserializationFailed);
                        }
                    }

                    if (importedAssetInfo != null)
                    {
                        var assetId = importedAssetInfo.Identifier?.AssetId;
                        if (trackingPathToAssetId != null && !string.IsNullOrEmpty(assetId))
                            trackingPathToAssetId[Path.GetFullPath(assetPath)] = assetId;
                        partialInfos.Add(importedAssetInfo);
                    }
                }
                catch (Exception e)
                {
                    migrationResult.AddFailure(MigrationFailureReason.ReadFailed);
                    Debug.LogWarning($"Unable to read tracking data from '{assetPath}'. File might be corrupted or locked: {e.Message}");
                    Utilities.DevLogException(e);
                }
            }

            // Aggregate by Asset Manager asset identifier
            // Multiple Unity files can belong to the same Asset Manager asset
            var aggregated = new Dictionary<AssetIdentifier, ImportedAssetInfo>();
            foreach (var partialInfo in partialInfos)
            {
                var identifier = partialInfo.Identifier;
                if (identifier == null)
                    continue;

                if (!aggregated.TryGetValue(identifier, out var existing))
                {
                    // First file for this asset - use it as the base
                    aggregated[identifier] = partialInfo;
                }
                else
                {
                    // Merge file infos from this partial info into the existing one
                    existing.FileInfos.AddRange(partialInfo.FileInfos);
                }
            }

            return aggregated.Values.ToList();
        }

        /// <summary>
        /// Reads a single tracking file, migrating if necessary.
        /// </summary>
        /// <param name="migrationResult">Indicates whether the file was migrated and success/failure (at most one of SuccessCount or FailureCount is non-zero).</param>
        public static ImportedAssetInfo ReadEntry(IIOProxy ioProxy, string filePath, out MigrationResult migrationResult)
        {
            migrationResult = default;

            if (ioProxy == null || string.IsNullOrEmpty(filePath) || !ioProxy.FileExists(filePath))
            {
                return null;
            }

            try
            {
                var content = ioProxy.FileReadAllText(filePath);

                if (string.IsNullOrEmpty(content))
                {
                    return null;
                }

                var currentVersionNumber = GetMaxSupportedVersion();
                var (major, minor) = ExtractSerializationVersion(content);

                if (major > currentVersionNumber)
                {
                    Debug.LogError($"Unsupported serialization version {major}.{minor} in tracking file '{filePath}'");
                    return null;
                }

                // Current version: read directly
                if (major == currentVersionNumber)
                {
                    return s_PersistenceVersions[major].ConvertToImportedAssetInfo(content);
                }

                // Older version: migrate, write new files, and delete legacy file
                var importedAssetInfo = MigrateAndWriteEntry(ioProxy, filePath, content, out var failureReason);
                if (importedAssetInfo != null)
                {
                    migrationResult.MigrationOccurred = true;
                    migrationResult.SuccessCount = 1;
                }
                else
                {
                    migrationResult.AddFailure(failureReason ?? MigrationFailureReason.DeserializationFailed);
                }

                return importedAssetInfo;
            }
            catch (NotSupportedException)
            {
                migrationResult.AddFailure(MigrationFailureReason.VersionUpgradeFailed);
                return null;
            }
            catch (Exception e)
            {
                migrationResult.AddFailure(MigrationFailureReason.ReadFailed);
                Debug.LogError($"Unable to read tracking data. Tracking file might be corrupted '{filePath}'");
                Utilities.DevLogException(e);
                return null;
            }
        }

        /// <summary>
        /// Migrates content from an older version to current format.
        /// Writes the new files, deletes the legacy file, and returns the ImportedAssetInfo.
        /// </summary>
        /// <param name="failureReason">When returning null, indicates why the migration failed.</param>
        static ImportedAssetInfo MigrateAndWriteEntry(IIOProxy ioProxy, string originalFilePath, string content, out MigrationFailureReason? failureReason)
        {
            failureReason = null;

            try
            {
                // Migrate to V3 (the last single-file format)
                var migratedContent = MigrateToLatestSingleFileFormat(content, out _);
                if (string.IsNullOrEmpty(migratedContent))
                {
                    failureReason = MigrationFailureReason.VersionUpgradeFailed;
                    return null;
                }

                // Convert V3 content to ImportedAssetInfo
                var (migratedMajor, _) = ExtractSerializationVersion(migratedContent);
                if (migratedMajor < 0 || migratedMajor >= s_PersistenceVersions.Length)
                {
                    failureReason = MigrationFailureReason.VersionUpgradeFailed;
                    return null;
                }

                var importedAssetInfo = s_PersistenceVersions[migratedMajor].ConvertToImportedAssetInfo(migratedContent);
                if (importedAssetInfo?.AssetData == null || importedAssetInfo.FileInfos == null || importedAssetInfo.FileInfos.Count == 0)
                {
                    failureReason = MigrationFailureReason.DeserializationFailed;
                    return null;
                }

                // Write using V4 per-Unity-file format
                var currentVersion = GetCurrentVersion();
                Persistence.WriteEntry(ioProxy, importedAssetInfo.AssetData as AssetData, importedAssetInfo.FileInfos);
                Utilities.DevLog($"Migrated asset '{importedAssetInfo.AssetData.Name}' with original path '{Path.GetFileName(originalFilePath)}'", DevLogHighlightColor.Cyan);

                // Delete the legacy file
                try
                {
                    if (ioProxy.FileExists(originalFilePath))
                    {
                        ioProxy.DeleteFile(originalFilePath);
                        Utilities.DevLog($"Deleted legacy tracking file: {Path.GetFileName(originalFilePath)} ({importedAssetInfo.AssetData.Name})", DevLogHighlightColor.Yellow);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Failed to delete legacy file '{originalFilePath}': {e.Message}");
                }

                return importedAssetInfo;
            }
            catch (Exception e)
            {
                failureReason = MigrationFailureReason.WriteFailed;
                Debug.LogWarning($"Failed to migrate tracking file '{originalFilePath}': {e.Message}");
                Utilities.DevLogException(e);
                return null;
            }
        }


        /// <summary>
        /// Migrates content from its current version to the latest single-file format version (V3).
        /// V4+ uses per-Unity-file format and cannot be represented as a single content string.
        /// The caller is responsible for converting the returned V3 content to ImportedAssetInfo
        /// and writing using Persistence.WriteEntry (which uses V4 format).
        /// </summary>
        static string MigrateToLatestSingleFileFormat(string content, out int originalMajorVersion)
        {
            var (major, minor) = ExtractSerializationVersion(content);
            originalMajorVersion = major;
            var currentVersion = GetMaxSupportedVersion();

            if (major > currentVersion)
            {
                throw new NotSupportedException($"Unsupported serialization version {major}.{minor}");
            }

            // V4+ uses per-Unity-file format; stop migration at V3 (the last single-file format)
            const int lastSingleFileVersion = 3;
            var targetVersion = Math.Min(currentVersion, lastSingleFileVersion);

            var migratedContent = content;
            for (var versionIndex = major; versionIndex < targetVersion; versionIndex++)
            {
                var nextVersion = s_PersistenceVersions[versionIndex + 1];

                migratedContent = MigrateBetweenSingleFileFormatVersions(
                    s_PersistenceVersions[versionIndex],
                    nextVersion,
                    migratedContent);

                if (string.IsNullOrEmpty(migratedContent))
                {
                    return null;
                }
            }

            return migratedContent;
        }

        /// <summary>
        /// Migrates content from one version to the next using single-file serialization.
        /// Only works for versions that support SerializeEntry (V0-V3).
        /// </summary>
        static string MigrateBetweenSingleFileFormatVersions(IPersistenceVersion fromVersion, IPersistenceVersion toVersion,
            string content)
        {
            var importedAssetInfo = fromVersion.ConvertToImportedAssetInfo(content);
            if (importedAssetInfo == null)
            {
                return null;
            }

            return toVersion.SerializeEntry(importedAssetInfo.AssetData as AssetData, importedAssetInfo.FileInfos);
        }

        /// <summary>
        /// Extracts the serialization version from JSON content.
        /// </summary>
        static (int major, int minor) ExtractSerializationVersion(string content)
        {
            var match = s_SerializationVersionRegex.Match(content);
            if (match.Success)
            {
                return (Int32.Parse(match.Groups[1].Value), Int32.Parse(match.Groups[2].Value));
            }

            return (0, 0); // when no serializationVersion is present, we're in version 0.0
        }

        /// <summary>
        /// Gets the maximum supported major version number.
        /// </summary>
        static int GetMaxSupportedVersion()
        {
            return s_PersistenceVersions[^1].MajorVersion;
        }
    }
}
