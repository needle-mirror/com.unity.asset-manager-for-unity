using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Manages persistence of imported asset information, including file system monitoring.
    /// </summary>
    interface IPersistenceManager : IService
    {
        /// <summary>
        /// Raised when a persisted asset entry is added or modified on disk.
        /// </summary>
        event EventHandler<ImportedAssetInfo> AssetEntryModified;

        /// <summary>
        /// Raised when a persisted asset entry is removed from disk.
        /// </summary>
        event EventHandler<string> AssetEntryRemoved;

        /// <summary>
        /// Reads all persisted asset entries from disk.
        /// </summary>
        IReadOnlyCollection<ImportedAssetInfo> ReadAllEntries();

        /// <summary>
        /// Writes an asset entry to disk.
        /// </summary>
        void WriteEntry(AssetData assetData, IEnumerable<ImportedFileInfo> fileInfos);

        /// <summary>
        /// Removes an asset entry from disk.
        /// </summary>
        void RemoveEntry(string assetId);
    }

    [Serializable]
    class PersistenceManager : BaseService<IPersistenceManager>, IPersistenceManager
    {

        [SerializeReference]
        IIOProxy m_IOProxy;

        [SerializeReference]
        IApplicationProxy m_ApplicationProxy;

        [SerializeReference]
        IMessageManager m_MessageManager;

        [SerializeReference]
        IAssetDatabaseProxy m_AssetDatabaseProxy;

        [NonSerialized]
        readonly Dictionary<string, string> m_FilePathToAssetIdCache = new();

        [NonSerialized]
        readonly Dictionary<string, int> m_AssetIdFileRefCounts = new();

        [NonSerialized]
        bool m_FilePathToAssetIdCachePopulated;

        [NonSerialized]
        bool m_IsEnabled;

        // Paths we removed from the file path cache (move or RemoveEntry); OnFileRemoved for these should not log a warning.
        [NonSerialized]
        readonly HashSet<string> m_PathsRemovedSilently = new();

        // Pending tracking-file reassignment warnings to batch into a single log message per operation.
        [NonSerialized]
        readonly List<(string filePath, string fromAssetId, string toAssetId)> m_PendingReassignmentWarnings = new();

        public event EventHandler<ImportedAssetInfo> AssetEntryModified;
        public event EventHandler<string> AssetEntryRemoved;

        public string TrackedFolder => Persistence.TrackedFolder;

        [ServiceInjection]
        public void Inject(IIOProxy ioProxy, IApplicationProxy applicationProxy, IMessageManager messageManager, IAssetDatabaseProxy assetDatabaseProxy)
        {
            m_IOProxy = ioProxy;
            m_ApplicationProxy = applicationProxy;
            m_MessageManager = messageManager;
            m_AssetDatabaseProxy = assetDatabaseProxy;
        }

        WatchedTrackingFolder m_TrackedFolderWatcher;

        [NonSerialized]
        IFileWatcher m_InjectedTrackedWatcher;

        public override void OnEnable()
        {
            base.OnEnable();

            Utilities.DevLog($"PersistenceManager.OnEnable: Tracked folder = {Persistence.TrackedFolder}", highlight: true);

            Persistence.EnsureTrackedFolderAndReadme(m_IOProxy);

            // Defer file path cache population to first use (OnFileRemoved, RemoveEntry) to avoid O(N) I/O on domain reload.
            m_FilePathToAssetIdCachePopulated = false;

            if (m_TrackedFolderWatcher == null)
            {
                var trackedWatcher = m_InjectedTrackedWatcher ?? new FileWatcher(m_IOProxy);
                m_TrackedFolderWatcher = new WatchedTrackingFolder(
                    Persistence.TrackedFolder,
                    trackedWatcher,
                    OnFileModified,
                    OnFileRemoved);
            }
            m_TrackedFolderWatcher.Start();

            m_AssetDatabaseProxy.PostprocessAllAssets += OnPostprocessAllAssets;
            m_IsEnabled = true;
        }

        public override void OnDisable()
        {
            base.OnDisable();

            m_AssetDatabaseProxy.PostprocessAllAssets -= OnPostprocessAllAssets;

            m_IsEnabled = false;
            m_TrackedFolderWatcher?.Dispose();
        }

        protected override void ValidateServiceDependencies()
        {
            base.ValidateServiceDependencies();
            m_IOProxy ??= ServicesContainer.instance.Get<IIOProxy>();
            m_ApplicationProxy ??= ServicesContainer.instance.Get<IApplicationProxy>();
            m_MessageManager ??= ServicesContainer.instance.Get<IMessageManager>();
        }

        public IReadOnlyCollection<ImportedAssetInfo> ReadAllEntries()
        {
            var trackingPathToAssetId = new Dictionary<string, string>();
            var allEntries = Persistence.ReadAllEntries(m_IOProxy, out var migrationResult, trackingPathToAssetId);
            PopulateFilePathCacheFromTrackingPaths(trackingPathToAssetId);
            if (migrationResult.MigrationOccurred)
            {
                ShowTrackingFilesMigratedMessages();
                AnalyticsSender.SendEvent(new TrackingFileMigrationEvent(
                    migrationResult.SuccessCount,
                    migrationResult.FailureCount,
                    migrationResult.VersionUpgradeFailedCount,
                    migrationResult.DeserializationFailedCount,
                    migrationResult.WriteFailedCount,
                    migrationResult.ReadFailedCount));
            }
            RecoverMovedTrackingFiles(allEntries);
            return allEntries;
        }

        void ShowTrackingFilesMigratedMessages()
        {
            const string consoleColorHex = "#FFA500"; // Orange

            var consoleMessage = $"<color={consoleColorHex}>Asset Manager for Unity: Tracking files have been migrated to the new format. For more information, see the documentation.</color>\n{PackageDocumentation.GetPackageManualPageUrl("tracking-files")}";
            Debug.LogWarning(consoleMessage);

            if (m_MessageManager != null)
            {
                // Rich text: orange path; <noparse> prevents <ProjectPath> from being parsed as a tag
                const string pathColorHex = "#FFA500";
                var pathFormatted = $"<color={pathColorHex}><noparse><ProjectPath>/uam</noparse></color>";
                var helpBoxContent = string.Format(L10n.Tr("Tracking files have been migrated to the new format. Ensure to commit the migrated files in {0} to your version control."), pathFormatted);
                m_MessageManager.SetHelpBoxMessage(new HelpBoxMessage(helpBoxContent,
                    RecommendedAction.OpenTrackingFilesMigrationDocumentation,
                    HelpBoxMessageType.Warning,
                    dismissable: true));
            }
        }

        void ShowPathTooLongErrorMessages(string affectedPath)
        {
            if (string.IsNullOrEmpty(affectedPath))
                return;

            var consoleMessage = $"Asset Manager for Unity: Failed to write tracking file(s) because the path exceeds the operating system's maximum path length. Affected asset: {affectedPath}";
            Debug.LogError(consoleMessage);

            if (m_MessageManager != null)
            {
                var helpBoxContent = L10n.Tr("Some tracking files could not be saved because the file path is too long. Consider moving the affected assets to a shorter path.");
                m_MessageManager.SetHelpBoxMessage(new HelpBoxMessage(helpBoxContent,
                    RecommendedAction.None,
                    HelpBoxMessageType.Error));
            }
        }

        public void WriteEntry(AssetData assetData, IEnumerable<ImportedFileInfo> fileInfos)
        {
            try
            {
                Persistence.WriteEntry(m_IOProxy, assetData, fileInfos);
                // All files written successfully - update cache for all
                UpdateFilePathCacheForWrittenFiles(assetData, fileInfos);
            }
            catch (TrackingFilePathTooLongException ex)
            {
                // Show error messages for each failed path
                foreach (var affectedPath in ex.AffectedPaths)
                {
                    ShowPathTooLongErrorMessages(affectedPath);
                }

                // Only update cache for successfully written files
                UpdateFilePathCacheForWrittenFiles(assetData, ex.SuccessfullyWrittenFileInfos);

                // Re-throw so caller can filter file infos for in-memory updates
                throw;
            }
        }

        public void RemoveEntry(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                return;

            EnsureFilePathCachePopulated();

            var pathsToDelete = new List<string>();
            foreach (var kvp in m_FilePathToAssetIdCache)
            {
                if (kvp.Value == assetId)
                    pathsToDelete.Add(kvp.Key);
            }

            foreach (var path in pathsToDelete)
                m_PathsRemovedSilently.Add(path);

            foreach (var path in pathsToDelete)
            {
                if (m_IOProxy.FileExists(path))
                    m_IOProxy.DeleteFile(path);
            }

            ScheduleDirectoryCleanup();
            RemoveFilePathCacheEntriesForAsset(assetId);
        }

        /// <summary>
        /// Schedules a single deferred pass to remove empty subdirectories under the tracked folder.
        /// Delegates to <see cref="Persistence.CleanEmptySubdirectories"/> which ensures
        /// multiple calls within the same editor frame are deduplicated so the scan runs only once.
        /// </summary>
        void ScheduleDirectoryCleanup()
        {
            Persistence.CleanEmptySubdirectories(m_IOProxy);
        }

        void RecoverMovedTrackingFiles(IReadOnlyCollection<ImportedAssetInfo> entries)
        {
            if (entries == null || entries.Count == 0)
                return;

            Utilities.DevLog($"Recovering moved tracking files (checking {entries.Count} entries)...", highlight: true);

            var recoveredCount = 0;
            foreach (var entry in entries)
            {
                if (entry?.FileInfos == null)
                    continue;

                foreach (var fileInfo in entry.FileInfos)
                {
                    if (string.IsNullOrEmpty(fileInfo.Guid))
                        continue;

                    var actualAssetPath = m_AssetDatabaseProxy.GuidToAssetPath(fileInfo.Guid);
                    if (string.IsNullOrEmpty(actualAssetPath))
                        continue;

                    var expectedTrackingPath = Persistence.GetTrackingFilePathForUnityAsset(actualAssetPath);
                    if (string.IsNullOrEmpty(expectedTrackingPath))
                        continue;

                    var normalizedExpectedPath = Path.GetFullPath(expectedTrackingPath);
                    if (m_FilePathToAssetIdCache.ContainsKey(normalizedExpectedPath))
                        continue;

                    Utilities.DevLog($"Found misplaced tracking file for GUID {fileInfo.Guid}: expected at {normalizedExpectedPath}. Recovering...", DevLogHighlightColor.Yellow);
                    MoveTrackingFile(fileInfo.OriginalPath, actualAssetPath);
                    recoveredCount++;
                }
            }

            if (recoveredCount > 0)
            {
                ScheduleDirectoryCleanup();
                Utilities.DevLog($"Recovery complete: {recoveredCount} tracking file(s) moved to correct locations.", highlight: true);
            }
            else
            {
                Utilities.DevLog($"Recovery complete: all tracking files are in correct locations.");
            }
        }

        void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (movedAssets == null || movedFromAssetPaths == null || movedAssets.Length != movedFromAssetPaths.Length)
                return;

            EnsureFilePathCachePopulated();

            for (int i = 0; i < movedAssets.Length; i++)
            {
                var oldPath = movedFromAssetPaths[i];
                var newPath = movedAssets[i];

                if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newPath))
                    continue;

                // Check if this asset is tracked by looking up the tracking file path in our cache.
                // Cache keys are normalized (Path.GetFullPath); use the same normalization for lookup.
                var trackingFilePath = Persistence.GetTrackingFilePathForUnityAsset(oldPath);
                if (string.IsNullOrEmpty(trackingFilePath))
                    continue;

                var normalizedTrackingPath = Path.GetFullPath(trackingFilePath);
                if (!m_FilePathToAssetIdCache.ContainsKey(normalizedTrackingPath))
                    continue;

                MoveTrackingFile(oldPath, newPath);
            }

            ScheduleDirectoryCleanup();
        }

        void MoveTrackingFile(string oldUnityAssetPath, string newUnityAssetPath)
        {
            // Update file path cache before moving the file so OnFileRemoved (from the delete) does not
            // find the old path in the file path cache and erroneously raise AssetEntryRemoved.
            UpdateFilePathCacheForMovedFile(oldUnityAssetPath, newUnityAssetPath);
            Persistence.MoveTrackingFile(m_IOProxy, m_AssetDatabaseProxy, oldUnityAssetPath, newUnityAssetPath);
        }

        void OnFileModified(object sender, FileEventArgs e)
        {
            try
            {
                Utilities.DevLog($"File modified: {e.FullPath}", highlight: true);

                var importedAssetInfo = Persistence.ReadEntry(m_IOProxy, e.FullPath, out var migrationResult);

                if (importedAssetInfo != null)
                {
                    UpdateFilePathCacheEntry(e.FullPath, importedAssetInfo.Identifier.AssetId);
                    FlushReassignmentWarnings();
                    AssetEntryModified?.Invoke(this, importedAssetInfo);
                }
                else
                {
                    Utilities.DevLogWarning($"OnFileModified: importedAssetInfo is null for {e.FullPath}", highlight: true);
                }

                if (migrationResult.MigrationOccurred)
                    ShowTrackingFilesMigratedMessages();
            }
            catch (Exception ex)
            {
                Utilities.DevLogException(ex);
            }
        }

        void OnFileRemoved(object sender, FileEventArgs e)
        {
            try
            {
                if (e == null || string.IsNullOrEmpty(e.FullPath))
                {
                    Utilities.DevLogWarning("OnFileRemoved: FileEventArgs or FullPath is null");
                    return;
                }

                if (m_IOProxy == null)
                {
                    Utilities.DevLogWarning("OnFileRemoved: m_IOProxy is null");
                    return;
                }

                Utilities.DevLog($"File removed: {e.FullPath}", DevLogHighlightColor.Red);

                var normalizedPath = Path.GetFullPath(e.FullPath);
                EnsureFilePathCachePopulated();
                var assetId = TryGetAssetIdFromFilePathCache(normalizedPath, out var assetIdFromFilePathCache)
                    ? assetIdFromFilePathCache
                    : Persistence.ExtractAssetIdFromFilePath(m_IOProxy, e.FullPath);

                HandleFileRemoval(assetId, normalizedPath, e.FullPath);
            }
            catch (Exception ex)
            {
                Utilities.DevLogException(ex);
            }
        }

        /// <summary>
        /// Tries to get the asset ID for the removed file from the file path cache.
        /// Use this when the file is already deleted and we can't read it (e.g. current per-Unity-file format).
        /// </summary>
        bool TryGetAssetIdFromFilePathCache(string normalizedPath, out string assetId)
        {
            return m_FilePathToAssetIdCache.TryGetValue(normalizedPath, out assetId);
        }

        void IncrementRefCount(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                return;

            m_AssetIdFileRefCounts[assetId] = m_AssetIdFileRefCounts.GetValueOrDefault(assetId, 0) + 1;
        }

        void DecrementRefCount(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                return;

            var count = m_AssetIdFileRefCounts.GetValueOrDefault(assetId, 0) - 1;
            if (count <= 0)
                m_AssetIdFileRefCounts.Remove(assetId);
            else
                m_AssetIdFileRefCounts[assetId] = count;
        }

        /// <summary>
        /// Adds a new entry to the file path cache and increments the ref count atomically.
        /// Does nothing if the path already exists in the cache.
        /// </summary>
        /// <returns>True if the entry was added, false if it already existed.</returns>
        bool AddCacheEntry(string normalizedPath, string assetId)
        {
            if (string.IsNullOrEmpty(normalizedPath) || string.IsNullOrEmpty(assetId))
                return false;

            if (!m_FilePathToAssetIdCache.TryAdd(normalizedPath, assetId))
                return false;

            IncrementRefCount(assetId);
            return true;
        }

        /// <summary>
        /// Removes an entry from the file path cache and decrements the ref count atomically.
        /// </summary>
        /// <returns>The asset ID that was removed, or null if the path was not in the cache.</returns>
        string RemoveCacheEntry(string normalizedPath)
        {
            if (string.IsNullOrEmpty(normalizedPath))
                return null;

            if (!m_FilePathToAssetIdCache.Remove(normalizedPath, out var assetId))
                return null;

            DecrementRefCount(assetId);
            return assetId;
        }

        /// <summary>
        /// Reassigns a cache entry from one asset to another, updating ref counts atomically.
        /// Use this when a tracking file is being reassigned to a different asset.
        /// </summary>
        /// <returns>The previous asset ID, or null if the path was not in the cache.</returns>
        string ReassignCacheEntry(string normalizedPath, string newAssetId)
        {
            if (string.IsNullOrEmpty(normalizedPath) || string.IsNullOrEmpty(newAssetId))
                return null;

            if (!m_FilePathToAssetIdCache.TryGetValue(normalizedPath, out var oldAssetId))
            {
                // Path not in cache, just add it
                AddCacheEntry(normalizedPath, newAssetId);
                return null;
            }

            if (string.Equals(oldAssetId, newAssetId, StringComparison.Ordinal))
                return null; // Same asset, no change needed

            DecrementRefCount(oldAssetId);
            m_FilePathToAssetIdCache[normalizedPath] = newAssetId;
            IncrementRefCount(newAssetId);
            return oldAssetId;
        }

        bool AssetHasRemainingFiles(string assetId)
        {
            return m_AssetIdFileRefCounts.GetValueOrDefault(assetId, 0) > 0;
        }

        void AddPendingReassignmentWarning(string filePath, string fromAssetId, string toAssetId)
        {
            m_PendingReassignmentWarnings.Add((filePath, fromAssetId, toAssetId));
        }

        void FlushReassignmentWarnings()
        {
            if (m_PendingReassignmentWarnings.Count == 0)
                return;

            var count = m_PendingReassignmentWarnings.Count;
            var sample = m_PendingReassignmentWarnings.Take(5)
                .Select(w => $"  '{w.filePath}': {w.fromAssetId} -> {w.toAssetId}");
            var message = $"Asset Manager: {count} tracking file(s) were reassigned between assets. " +
                "This can occur when multiple assets share the same files.\n" +
                string.Join("\n", sample);

            if (count > 5)
                message += $"\n  ... and {count - 5} more.";

            Debug.LogWarning(message);
            m_PendingReassignmentWarnings.Clear();
        }

        /// <summary>
        /// Updates the file path cache and raises AssetEntryRemoved when a tracking file is removed.
        /// If the asset still exists at another path (indicating a move, not deletion), no event is raised.
        /// </summary>
        void HandleFileRemoval(string assetId, string normalizedPath, string fullPathForLog)
        {
            RemoveCacheEntry(normalizedPath);

            if (!string.IsNullOrEmpty(assetId))
            {
                // Check if this asset exists at another path (indicating a move, not deletion)
                if (AssetHasRemainingFiles(assetId))
                {
                    Utilities.DevLog($"Asset {assetId} still exists at another path, ignoring removal for {Path.GetFileName(fullPathForLog)}", highlight: true);
                    return;
                }

                AssetEntryRemoved?.Invoke(this, assetId);
            }
            else
            {
                // Suppress warning when we caused the removal (move or RemoveEntry); file is gone so we can't read asset ID
                if (!m_PathsRemovedSilently.Remove(normalizedPath))
                    Utilities.DevLog($"OnFileRemoved: Could not extract assetId from {fullPathForLog}", DevLogHighlightColor.Yellow);
            }
        }

        internal void SetFileWatcher(IFileWatcher watcher)
        {
            m_InjectedTrackedWatcher = watcher;
        }

        /// <summary>
        /// Ensures the file path cache is populated (lazy-load). Called before operations that need it.
        /// </summary>
        void EnsureFilePathCachePopulated()
        {
            if (m_FilePathToAssetIdCachePopulated)
                return;

            PopulateFilePathCacheFromExistingFiles();
            m_FilePathToAssetIdCachePopulated = true;
        }

        /// <summary>
        /// Fills the file path cache from the (physical tracking file path, assetId) mapping collected during ReadAllEntries.
        /// </summary>
        void PopulateFilePathCacheFromTrackingPaths(Dictionary<string, string> trackingPathToAssetId)
        {
            if (trackingPathToAssetId == null)
                return;

            foreach (var kvp in trackingPathToAssetId)
            {
                if (!string.IsNullOrEmpty(kvp.Key) && !string.IsNullOrEmpty(kvp.Value))
                    UpdateFilePathCacheEntry(kvp.Key, kvp.Value);
            }

            FlushReassignmentWarnings();
            m_FilePathToAssetIdCachePopulated = true;
        }

        /// <summary>
        /// Populates the file path cache by reading all existing tracking files.
        /// Used when the file path cache is needed but ReadAllEntries has not been called yet (e.g. first OnFileRemoved or RemoveEntry).
        /// </summary>
        void PopulateFilePathCacheFromExistingFiles()
        {
            if (!m_IsEnabled || string.IsNullOrEmpty(Persistence.TrackedFolder))
                return;

            try
            {
                var trackingPathToAssetId = new Dictionary<string, string>();
                Persistence.ReadAllEntries(m_IOProxy, out _, trackingPathToAssetId);
                PopulateFilePathCacheFromTrackingPaths(trackingPathToAssetId);
            }
            catch (Exception e)
            {
                Utilities.DevLog($"Failed to populate file path cache from existing files: {e.Message}");
                Utilities.DevLogException(e);
            }
        }

        /// <summary>
        /// Updates the file path cache with file paths for files that were just written.
        /// </summary>
        void UpdateFilePathCacheForWrittenFiles(AssetData assetData, IEnumerable<ImportedFileInfo> fileInfos)
        {
            if (assetData?.Identifier?.AssetId == null || fileInfos == null)
                return;

            // Only update file path cache if tracked folder is initialized (OnEnable has been called)
            if (string.IsNullOrEmpty(Persistence.TrackedFolder))
                return;

            foreach (var fileInfo in fileInfos)
            {
                if (fileInfo != null && !string.IsNullOrEmpty(fileInfo.OriginalPath))
                {
                    var trackingFilePath = Persistence.GetTrackingFilePathForUnityAsset(fileInfo.OriginalPath);
                    if (!string.IsNullOrEmpty(trackingFilePath))
                    {
                        UpdateFilePathCacheEntry(trackingFilePath, assetData.Identifier.AssetId);
                    }
                }
            }

            FlushReassignmentWarnings();
        }

        /// <summary>
        /// Updates a single file path cache entry mapping file path to asset ID.
        /// Normalizes the file path before storing to ensure consistent lookups.
        /// If the path was previously mapped to a different asset, raises AssetEntryRemoved for the old asset
        /// only when the old asset has no remaining tracking files (mirrors HandleFileRemoval logic).
        /// </summary>
        void UpdateFilePathCacheEntry(string filePath, string assetId)
        {
            if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(assetId))
                return;

            // Normalize the path to ensure consistent lookups (resolves ".." components)
            var normalizedPath = Path.GetFullPath(filePath);

            // Check if this path was previously mapped to a different asset (tracking file reassignment, e.g. embedded deps shared between assets)
            var existingAssetId = ReassignCacheEntry(normalizedPath, assetId);
            if (existingAssetId != null)
            {
                Utilities.DevLog($"GUID conflict detected: path {normalizedPath} was mapped to asset {existingAssetId}, now remapping to {assetId}", DevLogHighlightColor.Yellow);

                AddPendingReassignmentWarning(filePath, existingAssetId, assetId);

                // Only remove the old asset if it has no remaining tracking files
                if (!AssetHasRemainingFiles(existingAssetId))
                {
                    AssetEntryRemoved?.Invoke(this, existingAssetId);
                }
            }
        }

        /// <summary>
        /// Updates the file path cache when a file is moved from old path to new path.
        /// If the old path is not in the file path cache, reads the tracking file to get the asset ID.
        /// </summary>
        void UpdateFilePathCacheForMovedFile(string oldUnityAssetPath, string newUnityAssetPath)
        {
            if (string.IsNullOrEmpty(oldUnityAssetPath) || string.IsNullOrEmpty(newUnityAssetPath))
                return;

            if (string.IsNullOrEmpty(Persistence.TrackedFolder))
                return;

            var oldTrackingFilePath = Persistence.GetTrackingFilePathForUnityAsset(oldUnityAssetPath);
            var newTrackingFilePath = Persistence.GetTrackingFilePathForUnityAsset(newUnityAssetPath);

            if (string.IsNullOrEmpty(oldTrackingFilePath) || string.IsNullOrEmpty(newTrackingFilePath))
                return;

            var normalizedOldPath = Path.GetFullPath(oldTrackingFilePath);
            var normalizedNewPath = Path.GetFullPath(newTrackingFilePath);

            string assetId = null;

            // Try to get from cache first, then fall back to reading the file
            if (m_FilePathToAssetIdCache.TryGetValue(normalizedOldPath, out var assetIdFromFilePathCache))
            {
                assetId = assetIdFromFilePathCache;
                RemoveCacheEntry(normalizedOldPath);
                m_PathsRemovedSilently.Add(normalizedOldPath);
            }
            else
            {
                try
                {
                    var importedAssetInfo = Persistence.ReadEntry(m_IOProxy, normalizedOldPath, out var migrationResult);
                    if (importedAssetInfo?.Identifier?.AssetId != null)
                    {
                        assetId = importedAssetInfo.Identifier.AssetId;
                        m_PathsRemovedSilently.Add(normalizedOldPath);
                    }

                    if (migrationResult.MigrationOccurred)
                        ShowTrackingFilesMigratedMessages();
                }
                catch
                {
                    return;
                }
            }

            if (!string.IsNullOrEmpty(assetId))
            {
                AddCacheEntry(normalizedNewPath, assetId);
            }
        }

        /// <summary>
        /// Removes all file path cache entries for files belonging to the specified asset.
        /// </summary>
        void RemoveFilePathCacheEntriesForAsset(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
                return;

            // Find all file path cache entries with this asset ID and remove them
            var keysToRemove = new List<string>();
            foreach (var kvp in m_FilePathToAssetIdCache)
            {
                if (kvp.Value == assetId)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                m_FilePathToAssetIdCache.Remove(key);
            }

            m_AssetIdFileRefCounts.Remove(assetId);
        }

    }
}

