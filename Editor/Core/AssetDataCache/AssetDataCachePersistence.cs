using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Handles reading and writing AssetDataCache entries to the Library folder.
    /// </summary>
    static class AssetDataCachePersistence
    {
        static readonly Regex s_CacheVersionRegex =
            new("\"cacheVersion\"\\s*:\\s*(\\d+)",
                RegexOptions.None,
                TimeSpan.FromMilliseconds(100));

        const string k_FileSearchPattern = "*.json";

        static readonly string s_DefaultCacheFolder =
            Path.Combine(
                Application.dataPath,
                "..",
                "Library",
                AssetManagerCoreConstants.PackageName,
                "AssetDataCache");

        static string s_OverrideCacheFolder = null; // for testing purposes
        internal static void OverrideCacheFolder(string cacheFolder) => s_OverrideCacheFolder = cacheFolder;

        static string CacheFolder
        {
            get
            {
                if (!string.IsNullOrEmpty(s_OverrideCacheFolder))
                {
                    return s_OverrideCacheFolder;
                }

                return s_DefaultCacheFolder;
            }
        }

        /// <summary>
        /// Reads all cache entries from the cache folder.
        /// </summary>
        public static IReadOnlyCollection<AssetDataCacheEntry> ReadAllEntries(IIOProxy ioProxy)
        {
            if (ioProxy == null)
            {
                Utilities.DevLogError("Null IIOProxy service");
                return Array.Empty<AssetDataCacheEntry>();
            }

            if (!ioProxy.DirectoryExists(CacheFolder))
            {
                return Array.Empty<AssetDataCacheEntry>();
            }

            var entries = new List<AssetDataCacheEntry>();
            try
            {
                foreach (var filePath in ioProxy.EnumerateFiles(CacheFolder, k_FileSearchPattern, SearchOption.TopDirectoryOnly))
                {
                    var entry = ReadEntryFromPath(ioProxy, filePath);
                    if (entry != null)
                    {
                        entries.Add(entry);
                    }
                }
            }
            catch (IOException e)
            {
                Utilities.DevLogError($"Failed to enumerate files in '{CacheFolder}': {e.Message}");
            }

            return entries;
        }

        /// <summary>
        /// Reads a single cache entry by asset ID.
        /// Returns null if the entry doesn't exist or is outdated.
        /// </summary>
        public static AssetDataCacheEntry ReadEntry(IIOProxy ioProxy, string assetId)
        {
            if (ioProxy == null)
            {
                Utilities.DevLogError("Null IIOProxy service");
                return null;
            }

            var filePath = GetFilenameFor(assetId);
            return ReadEntryFromPath(ioProxy, filePath);
        }

        static AssetDataCacheEntry ReadEntryFromPath(IIOProxy ioProxy, string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !ioProxy.FileExists(filePath))
            {
                return null;
            }

            string content = null;
            try
            {
                content = ioProxy.FileReadAllText(filePath);
            }
            catch (IOException e)
            {
                Utilities.DevLogError($"Failed to read file '{filePath}': {e.Message}");
                return null;
            }

            if (string.IsNullOrEmpty(content))
            {
                return null;
            }

            try
            {
                var version = ExtractCacheVersion(content);

                // Skip entries that don't match the current version
                if (version != AssetDataCacheEntry.CurrentCacheVersion)
                {
                    Utilities.DevLogWarning($"AssetDataCache version {version} does not match current version {AssetDataCacheEntry.CurrentCacheVersion}. Skipping '{filePath}'.");
                    return null;
                }

                return JsonUtility.FromJson<AssetDataCacheEntry>(content);
            }
            catch (Exception e)
            {
                Utilities.DevLogError($"Failed to parse AssetDataCache file '{filePath}': {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Writes a cache entry to the cache folder.
        /// Always writes using the current version.
        /// </summary>
        public static void WriteEntry(IIOProxy ioProxy, AssetDataCacheEntry entry)
        {
            if (ioProxy == null)
            {
                Utilities.DevLogError("Null IIOProxy service");
                return;
            }

            if (entry == null || string.IsNullOrEmpty(entry.assetId))
            {
                Utilities.DevLogWarning("Cannot write AssetDataCache entry: entry or assetId is null/empty");
                return;
            }

            entry.cacheVersion = AssetDataCacheEntry.CurrentCacheVersion;
            entry.cachedAt = DateTime.UtcNow.ToString("o");

            var filePath = GetFilenameFor(entry.assetId);
            var content = JsonUtility.ToJson(entry, prettyPrint: false);

            try
            {
                var directory = Path.GetDirectoryName(filePath);
                ioProxy.EnsureDirectoryExists(directory);
                ioProxy.FileWriteAllText(filePath, content);
            }
            catch (IOException e)
            {
                Utilities.DevLogError($"Failed to write file '{filePath}': {e.Message}");
            }
        }

        /// <summary>
        /// Removes a cache entry for the specified asset ID.
        /// </summary>
        public static void RemoveEntry(IIOProxy ioProxy, string assetId)
        {
            if (ioProxy == null)
            {
                Utilities.DevLogError("Null IIOProxy service");
                return;
            }

            if (string.IsNullOrEmpty(assetId))
                return;

            var filePath = GetFilenameFor(assetId);
            if (!ioProxy.FileExists(filePath))
                return;

            try
            {
                ioProxy.DeleteFile(filePath);
            }
            catch (IOException e)
            {
                Utilities.DevLogError($"Failed to delete file '{filePath}': {e.Message}");
            }
        }

        /// <summary>
        /// Checks if a cache entry exists for the specified asset ID.
        /// </summary>
        public static bool EntryExists(IIOProxy ioProxy, string assetId)
        {
            if (ioProxy == null || string.IsNullOrEmpty(assetId))
            {
                return false;
            }

            var filePath = GetFilenameFor(assetId);
            return ioProxy.FileExists(filePath);
        }

        /// <summary>
        /// Clears all cache entries.
        /// </summary>
        public static void ClearAllEntries(IIOProxy ioProxy)
        {
            if (ioProxy == null)
            {
                Utilities.DevLogError("Null IIOProxy service");
                return;
            }

            if (!ioProxy.DirectoryExists(CacheFolder))
            {
                return;
            }

            try
            {
                foreach (var filePath in ioProxy.EnumerateFiles(CacheFolder, k_FileSearchPattern, SearchOption.TopDirectoryOnly))
                {
                    if (ioProxy.FileExists(filePath))
                    {
                        try
                        {
                            ioProxy.DeleteFile(filePath);
                        }
                        catch (IOException e)
                        {
                            Utilities.DevLogError($"Failed to delete file '{filePath}': {e.Message}");
                        }
                    }
                }
            }
            catch (IOException e)
            {
                Utilities.DevLogError($"Failed to enumerate files in '{CacheFolder}': {e.Message}");
            }
        }

        static string GetFilenameFor(string assetId)
        {
            return Path.Combine(CacheFolder, $"{assetId}.json");
        }

        static int ExtractCacheVersion(string content)
        {
            var match = s_CacheVersionRegex.Match(content);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var version))
            {
                return version;
            }

            // Default to version 1 if not found (backwards compatibility)
            return 1;
        }
    }
}
