using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    static class Persistence
    {
        const int k_SerializationMajorVersion = 1;
        const int k_SerializationMinorVersion = 0;

        static Regex s_SerializationVersionRegex =
            new ("\"serializationVersion\"\\s*:\\s*\\[\\s*(\\d+)\\s*,\\s*(\\d+)\\s*\\]",
                RegexOptions.None,
                TimeSpan.FromMilliseconds(100));

        static readonly string s_TrackedFolder =
            Path.Combine(
                Application.dataPath,
                "..",
                "ProjectSettings",
                "Packages",
                AssetManagerCoreConstants.PackageName,
                "ImportedAssetInfo");

        [Serializable]
        class TrackedAssetVersionPersisted
        {
            [SerializeField]
            public string versionId;

            [SerializeField]
            public string name;

            [SerializeField]
            public int sequenceNumber;

            [SerializeField]
            public int parentSequenceNumber;

            [SerializeField]
            public string changelog;

            [SerializeField]
            public AssetType assetType;

            [SerializeField]
            public string status;

            [SerializeField]
            public string description;

            [SerializeField]
            public string created;

            [SerializeField]
            public string updated;

            [SerializeField]
            public string createdBy;

            [SerializeField]
            public string updatedBy;

            [SerializeField]
            public string previewFilePath;

            [SerializeField]
            public bool isFrozen;

            [SerializeField]
            public List<string> tags;
        }

        [Serializable]
        class TrackedAssetIdentifierPersisted
        {
            [SerializeField]
            public string organizationId;

            [SerializeField]
            public string projectId;

            [SerializeField]
            public string assetId;

            [SerializeField]
            public string versionId;
        }

        [Serializable]
        class TrackedAssetPersisted : TrackedAssetVersionPersisted
        {
            [SerializeField]
            public int[] serializationVersion;

            [SerializeField]
            public string organizationId;

            [SerializeField]
            public string projectId;

            [SerializeField]
            public string assetId;

            [SerializeField]
            public List<TrackedAssetIdentifierPersisted> dependencyAssets;

            [SerializeField]
            public List<TrackedFilePersisted> files;

            [SerializeReference]
            public List<IMetadata> metadata;
        }

        [Serializable]
        class TrackedFilePersisted
        {
            [SerializeField]
            public string path; // key

            [SerializeField]
            public string trackedUnityGuid; // only set if this is a tracked asset

            [SerializeField]
            public string extension;

            [SerializeField]
            public bool available;

            [SerializeField]
            public string description;

            [SerializeField]
            public long fileSize;

            [SerializeField]
            public List<string> tags = new();

            [SerializeField]
            public string checksum;

            [SerializeField]
            public long timestamp;

            [SerializeField]
            public string metaFileChecksum;

            [SerializeField]
            public long metaFileTimestamp;
        }

        static AssetIdentifier ExtractAssetIdentifier(TrackedAssetPersisted trackedAsset)
        {
            return new AssetIdentifier(trackedAsset.organizationId, trackedAsset.projectId, trackedAsset.assetId,
                trackedAsset.versionId);
        }

        class ReadCache
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

        static AssetDataFile ConvertFile(TrackedFilePersisted trackedFile)
        {
            if (trackedFile == null)
            {
                return null;
            }

            var assetDataFile = new AssetDataFile(
                trackedFile.path,
                trackedFile.extension,
                null,
                trackedFile.description,
                trackedFile.tags,
                trackedFile.fileSize,
                trackedFile.available);

            return assetDataFile;
        }

        static ImportedAssetInfo Convert(TrackedAssetPersisted trackedAsset, ReadCache cache)
        {
            var assetIdentifier = ExtractAssetIdentifier(trackedAsset);
            var assetData = cache.GetAssetDataFor(assetIdentifier);
            assetData.FillFromPersistence(
                new AssetIdentifier(trackedAsset.organizationId,
                    trackedAsset.projectId,
                    trackedAsset.assetId,
                    trackedAsset.versionId),
                trackedAsset.sequenceNumber,
                trackedAsset.parentSequenceNumber,
                trackedAsset.changelog,
                trackedAsset.name,
                trackedAsset.assetType,
                trackedAsset.status,
                trackedAsset.description,
                DateTime.Parse(trackedAsset.created, null, DateTimeStyles.RoundtripKind),
                DateTime.Parse(trackedAsset.updated, null, DateTimeStyles.RoundtripKind),
                trackedAsset.createdBy,
                trackedAsset.updatedBy,
                trackedAsset.previewFilePath,
                trackedAsset.isFrozen,
                trackedAsset.tags,
                trackedAsset.files
                    .Select(x => ConvertFile(x)),
                trackedAsset.dependencyAssets
                    .Select(x => new AssetIdentifier(x.organizationId, x.projectId, x.assetId, x.versionId)),
                trackedAsset.metadata);

            return new ImportedAssetInfo(
                assetData,
                trackedAsset.files
                    .Where(x => !string.IsNullOrEmpty(x.trackedUnityGuid))
                    .Select(x => new ImportedFileInfo(x.trackedUnityGuid, x.path, x.checksum, x.timestamp, x.metaFileChecksum, x.metaFileTimestamp)));
        }

        static TrackedAssetIdentifierPersisted Convert(AssetIdentifier identifier)
        {
            return new TrackedAssetIdentifierPersisted()
            {
                organizationId = identifier.OrganizationId,
                projectId = identifier.ProjectId,
                assetId = identifier.AssetId,
                versionId = identifier.Version
            };
        }

        static string ConvertPersistenceLegacyToCurrent(PersistenceLegacy persistenceLegacyReader, IIOProxy ioProxy, string content)
        {
            string persistedCurrentContent = null;
            var importedAssetInfo = persistenceLegacyReader.ReadEntry(content);
            if (importedAssetInfo != null)
            {
                Persistence.WriteEntry(ioProxy, importedAssetInfo.AssetData as AssetData, importedAssetInfo.FileInfos);

                persistedCurrentContent = Persistence.SerializeEntry(importedAssetInfo.AssetData as AssetData, importedAssetInfo.FileInfos);
                Persistence.WriteEntry(ioProxy, importedAssetInfo.AssetData.Identifier.AssetId, persistedCurrentContent);
            }

            return persistedCurrentContent;
        }

        public static IReadOnlyCollection<ImportedAssetInfo> ReadAllEntries(IIOProxy ioProxy)
        {
            if (ioProxy == null)
            {
                Utilities.DevLogError("Null IIOProxy service");
                return Array.Empty<ImportedAssetInfo>();
            }

            if (!ioProxy.DirectoryExists(s_TrackedFolder))
            {
                return Array.Empty<ImportedAssetInfo>();
            }

            // Read data as-is into persistence structure data
            PersistenceLegacy persistenceLegacyReader = null;
            Dictionary<TrackedAssetPersisted, string> trackedAssets = new();
            foreach (var assetPath in ioProxy.EnumerateFiles(s_TrackedFolder, "*", SearchOption.TopDirectoryOnly))
            {
                TrackedAssetPersisted trackedAsset = null;

                try
                {
                    var content = ioProxy.FileReadAllText(assetPath);
                    var (major, minor) = ExtractSerializationVersion(content);

                    if (major == 0 && minor == 0)
                    {
                        persistenceLegacyReader ??= new PersistenceLegacy();

                        // Conversion between persistence version if needed
                        content = ConvertPersistenceLegacyToCurrent(persistenceLegacyReader, ioProxy, content);
                    }

                    trackedAsset = JsonUtility.FromJson<TrackedAssetPersisted>(content);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Unable to read tracking data. Tracking file might be corrupted '{assetPath}'");
                    Utilities.DevLogException(e);
                }

                if (trackedAsset != null)
                {
                    trackedAssets.Add(trackedAsset, assetPath);
                }
            }

            // Convert to ImportedAssetInfo
            var cache = new ReadCache();
            var imported = new List<ImportedAssetInfo>();
            foreach (var trackedAsset in trackedAssets)
            {
                ImportedAssetInfo info = null;
                try
                {
                    info = Convert(trackedAsset.Key, cache);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Unable to convert tracked data to import info. Tracking file might be corrupted '{trackedAsset.Value}'");
                    Utilities.DevLogException(e);
                }

                if (info != null)
                {
                    imported.Add(info);
                }
            }

            return imported;
        }

        static void FillVersionFrom(TrackedAssetVersionPersisted version, AssetData assetData)
        {
            version.versionId = assetData.Identifier.Version;
            version.name = assetData.Name;
            version.sequenceNumber = assetData.SequenceNumber;
            version.parentSequenceNumber = assetData.ParentSequenceNumber;
            version.changelog = assetData.Changelog;
            version.assetType = assetData.AssetType;
            version.status = assetData.Status;
            version.description = assetData.Description;
            version.created = assetData.Created?.ToString("o");
            version.updated = assetData.Updated?.ToString("o");
            version.createdBy = assetData.CreatedBy;
            version.updatedBy = assetData.UpdatedBy;
            version.previewFilePath = assetData.PreviewFilePath;
            version.isFrozen = assetData.IsFrozen;
            version.tags = assetData.Tags?.ToList();
        }

        static TrackedFilePersisted ConvertToFile(AssetDataFile assetDataFile, ImportedFileInfo fileInfo)
        {
            var trackedFile = new TrackedFilePersisted();
            trackedFile.path = assetDataFile.Path;
            trackedFile.trackedUnityGuid = fileInfo?.Guid;
            trackedFile.extension = assetDataFile.Extension;
            trackedFile.available = assetDataFile.Available;
            trackedFile.description = assetDataFile.Description;
            trackedFile.fileSize = assetDataFile.FileSize;
            trackedFile.tags = assetDataFile.Tags.ToList();
            trackedFile.checksum = fileInfo?.Checksum;
            trackedFile.timestamp = fileInfo?.Timestamp ?? 0L;
            trackedFile.metaFileChecksum = fileInfo?.MetaFileChecksum;
            trackedFile.metaFileTimestamp = fileInfo?.MetalFileTimestamp ?? 0L;

            return trackedFile;
        }

        public static void RemoveEntry(IIOProxy ioProxy, string assetId)
        {
            ioProxy.DeleteFile(GetFilenameFor(assetId));
        }

        public static void WriteEntry(IIOProxy ioProxy, AssetData assetData, IEnumerable<ImportedFileInfo> fileInfos)
        {
            var fileContent = SerializeEntry(assetData, fileInfos);
            WriteEntry(ioProxy, assetData.Identifier.AssetId, fileContent);
        }

        static string SerializeEntry(AssetData assetData, IEnumerable<ImportedFileInfo> fileInfos)
        {
            var trackedAsset = new TrackedAssetPersisted();
            trackedAsset.serializationVersion = new[] { k_SerializationMajorVersion, k_SerializationMinorVersion };

            FillVersionFrom(trackedAsset, assetData);
            trackedAsset.organizationId = assetData.Identifier.OrganizationId;
            trackedAsset.projectId = assetData.Identifier.ProjectId;
            trackedAsset.assetId = assetData.Identifier.AssetId;
            trackedAsset.dependencyAssets = assetData.Dependencies
                .Select(Convert)
                .ToList();

            var importedFileInfos =
                fileInfos.ToDictionary(x => x.OriginalPath.Replace('\\', '/'), x => x);

            trackedAsset.files = assetData.SourceFiles
                .Select(x => x as AssetDataFile)
                .Where(x => x != null)
                .Select(x => ConvertToFile(x, importedFileInfos.GetValueOrDefault(x.Path)))
                .ToList();

            trackedAsset.metadata = assetData.Metadata;

            return SerializeEntry(trackedAsset);
        }

        static string SerializeEntry(TrackedAssetPersisted trackedAsset)
        {
            return JsonUtility.ToJson(trackedAsset);
        }

        static void WriteEntry(IIOProxy ioProxy, string assetId, string fileContent)
        {
            var importInfoFilePath = GetFilenameFor(assetId);
            try
            {
                var directoryPath = Path.GetDirectoryName(importInfoFilePath);
                if (!ioProxy.DirectoryExists(directoryPath))
                {
                    ioProxy.CreateDirectory(directoryPath);
                }

                ioProxy.FileWriteAllText(importInfoFilePath, fileContent);
            }
            catch (IOException e)
            {
                Debug.Log($"Couldn't write imported asset info to {importInfoFilePath} :\n{e}.");
            }
        }

        static string GetFilenameFor(string assetId)
        {
            return Path.Combine(s_TrackedFolder, assetId);
        }

        static (int major, int minor) ExtractSerializationVersion(string content)
        {
            var match = s_SerializationVersionRegex.Match(content);
            if (match.Success)
            {
                return (Int32.Parse(match.Groups[1].Value), Int32.Parse(match.Groups[2].Value));
            }

            return (0, 0); // when no serializationVersion is present, we're in version 0.0
        }

    }
}
