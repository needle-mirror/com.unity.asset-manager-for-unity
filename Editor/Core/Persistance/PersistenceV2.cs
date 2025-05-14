using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    class PersistenceV2 : IPersistenceVersion
    {
        public int MajorVersion => 2;
        public int MinorVersion => 0;

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

            [SerializeField]
            public string versionLabel;
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

            [SerializeField]
            public List<TrackedDatasetPersisted> datasets;

            [SerializeReference]
            public List<IMetadata> metadata;
        }

        [Serializable]
        class TrackedFilePersisted
        {
            [SerializeField]
            public string datasetId;

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

        [Serializable]
        class TrackedDatasetPersisted
        {
            [SerializeField]
            public string id;

            [SerializeField]
            public string name;

            [SerializeField]
            public List<string> systemTags;

            [SerializeField]
            public List<string> fileKeys;
        }

        public ImportedAssetInfo ConvertToImportedAssetInfo(string content)
        {
            var trackedAsset = JsonUtility.FromJson<TrackedAssetPersisted>(content);
            var cache = new Persistence.ReadCache();

            return Convert(trackedAsset, cache);
        }

        public string SerializeEntry(AssetData assetData, IEnumerable<ImportedFileInfo> fileInfos)
        {
            var trackedAsset = new TrackedAssetPersisted();
            trackedAsset.serializationVersion = new[] { MajorVersion, MinorVersion };

            FillVersionFrom(trackedAsset, assetData);
            trackedAsset.organizationId = assetData.Identifier.OrganizationId;
            trackedAsset.projectId = assetData.Identifier.ProjectId;
            trackedAsset.assetId = assetData.Identifier.AssetId;
            trackedAsset.dependencyAssets = assetData.Dependencies
                .Select(Convert)
                .ToList();

            var importedFileInfos =
                fileInfos.ToDictionary(x => x.OriginalPath.Replace('\\', '/'), x => x);

            var files = new List<TrackedFilePersisted>();
            foreach (var dataset in assetData.Datasets)
            {
                files.AddRange(dataset.Files
                    .Select(x => ConvertToFile(dataset.Id, x, importedFileInfos.GetValueOrDefault(x.Path))));
            }

            trackedAsset.files = files;
            trackedAsset.datasets = assetData.Datasets
                .Select(x => new TrackedDatasetPersisted
                {
                    id = x.Id,
                    name = x.Name,
                    systemTags = x.SystemTags.ToList(),
                    fileKeys = x.Files.Select(f => f.Path).ToList()
                })
                .ToList();

            trackedAsset.metadata = assetData.Metadata.ToList();

            return SerializeEntry(trackedAsset);
        }

        static AssetIdentifier ExtractAssetIdentifier(TrackedAssetPersisted trackedAsset)
        {
            return new AssetIdentifier(trackedAsset.organizationId, trackedAsset.projectId, trackedAsset.assetId,
                trackedAsset.versionId);
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

        static IEnumerable<AssetDataset> ReconstructDatasets(List<TrackedDatasetPersisted> datasets, List<TrackedFilePersisted> files)
        {
            var datasetFiles = files.ToDictionary(x => x.path, x => x);
            foreach (var dataset in datasets)
            {
                var datasetFilesPaths = dataset.fileKeys;
                var datasetFilesData = datasetFilesPaths.Select(x => datasetFiles.GetValueOrDefault(x)).ToList();
                var datasetFilesDataFiltered = datasetFilesData.Where(x => x != null).ToList();
                var datasetFilesConverted = datasetFilesDataFiltered.Select(ConvertFile).ToList();
                yield return new AssetDataset(dataset.id, dataset.name, dataset.systemTags, datasetFilesConverted);
            }
        }

        static ImportedAssetInfo Convert(TrackedAssetPersisted trackedAsset, Persistence.ReadCache cache)
        {
            var assetIdentifier = ExtractAssetIdentifier(trackedAsset);
            var assetData = cache.GetAssetDataFor(assetIdentifier);

            var datasets = ReconstructDatasets(trackedAsset.datasets, trackedAsset.files);
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
                DateTime.Parse(trackedAsset.created, DateTimeFormatInfo.CurrentInfo, DateTimeStyles.RoundtripKind),
                DateTime.Parse(trackedAsset.updated, DateTimeFormatInfo.CurrentInfo, DateTimeStyles.RoundtripKind),
                trackedAsset.createdBy,
                trackedAsset.updatedBy,
                trackedAsset.previewFilePath,
                trackedAsset.isFrozen,
                trackedAsset.tags,
                datasets,
                trackedAsset.dependencyAssets
                    .Select(x => new AssetIdentifier(x.organizationId, x.projectId, x.assetId, x.versionId, x.versionLabel)),
                trackedAsset.metadata);

            return new ImportedAssetInfo(
                assetData,
                trackedAsset.files
                    .Where(x => !string.IsNullOrEmpty(x.trackedUnityGuid))
                    .Select(x => new ImportedFileInfo(x.datasetId, x.trackedUnityGuid, x.path, x.checksum, x.timestamp, x.metaFileChecksum, x.metaFileTimestamp)));
        }

        static TrackedAssetIdentifierPersisted Convert(AssetIdentifier identifier)
        {
            return new TrackedAssetIdentifierPersisted()
            {
                organizationId = identifier.OrganizationId,
                projectId = identifier.ProjectId,
                assetId = identifier.AssetId,
                versionId = identifier.Version,
                versionLabel = identifier.VersionLabel
            };
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

        static TrackedFilePersisted ConvertToFile(string datasetId, BaseAssetDataFile assetDataFile, ImportedFileInfo fileInfo)
        {
            return new TrackedFilePersisted
            {
                datasetId = datasetId,
                path = assetDataFile.Path,
                trackedUnityGuid = fileInfo?.Guid,
                extension = assetDataFile.Extension,
                available = assetDataFile.Available,
                description = assetDataFile.Description,
                fileSize = assetDataFile.FileSize,
                tags = assetDataFile.Tags?.ToList(),
                checksum = fileInfo?.Checksum,
                timestamp = fileInfo?.Timestamp ?? 0L,
                metaFileChecksum = fileInfo?.MetaFileChecksum,
                metaFileTimestamp = fileInfo?.MetalFileTimestamp ?? 0L
            };
        }

        static string SerializeEntry(TrackedAssetPersisted trackedAsset)
        {
            return JsonUtility.ToJson(trackedAsset);
        }
    }
}
