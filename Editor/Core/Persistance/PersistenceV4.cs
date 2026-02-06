using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// First iteration of per-Unity-file tracking.
    /// This format uses one tracking file per Unity asset file (not per Asset Manager asset).
    /// Files are stored using path-based naming that mirrors the Unity project structure.
    /// </summary>
    class PersistenceV4 : IPersistenceVersion
    {
        public int MajorVersion => 4;
        public int MinorVersion => 0;

        /// <summary>
        /// Per-Unity-file tracking file schema.
        /// One file per Unity asset (not per Asset Manager asset).
        /// </summary>
        [Serializable]
        class TrackedUnityAssetPersisted
        {
            [SerializeField]
            public int[] serializationVersion;

            [SerializeField]
            public string path;

            [SerializeField]
            public string assetName;

            [SerializeField]
            public string assetId;

            [SerializeField]
            public string datasetId;

            [SerializeField]
            public string projectId;

            [SerializeField]
            public string organizationId;

            [SerializeField]
            public string versionId;

            [SerializeField]
            public int sequenceNumber;

            [SerializeField]
            public string updated;

            [SerializeField]
            public long timestamp;

            [SerializeField]
            public string checksum;

            [SerializeField]
            public string metaFileChecksum;

            [SerializeField]
            public long metaFileTimestamp;

            [SerializeField]
            public string unityGUID;
        }

        public ImportedAssetInfo ConvertToImportedAssetInfo(string content)
        {
            var trackedAsset = JsonUtility.FromJson<TrackedUnityAssetPersisted>(content);
            return trackedAsset == null ? null : Convert(trackedAsset);
        }

        public string SerializeEntry(AssetData assetData, IEnumerable<ImportedFileInfo> fileInfos)
        {
            // PersistenceV4 creates one file per Unity asset file
            // This method is called during migration from previous versions
            // For V4, we would write multiple files (one per fileInfo), but migration
            // will be handled separately. This method returns null to indicate
            // that V4 should not be used for migration yet.
            return null;
        }

        /// <summary>
        /// Serializes a single Unity asset file to a tracking file.
        /// </summary>
        public string SerializeEntryForFile(AssetData assetData, ImportedFileInfo fileInfo)
        {
            if (assetData == null || fileInfo == null)
            {
                return null;
            }

            var trackedAsset = new TrackedUnityAssetPersisted
            {
                serializationVersion = new[] { MajorVersion, MinorVersion },
                path = fileInfo.OriginalPath,
                assetName = assetData.Name,
                assetId = assetData.Identifier.AssetId,
                datasetId = fileInfo.DatasetId,
                projectId = assetData.Identifier.ProjectId,
                organizationId = assetData.Identifier.OrganizationId,
                versionId = assetData.Identifier.Version,
                sequenceNumber = assetData.SequenceNumber,
                updated = assetData.Updated.HasValue ? assetData.Updated.Value.ToString("o") : null,
                timestamp = fileInfo.Timestamp,
                checksum = fileInfo.Checksum,
                metaFileChecksum = fileInfo.MetaFileChecksum,
                metaFileTimestamp = fileInfo.MetaFileTimestamp,
                unityGUID = fileInfo.Guid
            };

            return JsonUtility.ToJson(trackedAsset, true);
        }

        static ImportedAssetInfo Convert(TrackedUnityAssetPersisted trackedAsset)
        {
            if (trackedAsset == null)
            {
                return null;
            }

            var assetIdentifier = new AssetIdentifier(
                trackedAsset.organizationId,
                trackedAsset.projectId,
                trackedAsset.assetId,
                trackedAsset.versionId);

            var assetData = new AssetData();

            // Fill AssetData with only the essential fields stored in tracking files.
            // Additional fields (status, dependencies, metadata, etc.) are not stored in tracking files
            // and will be populated from the UI cache when needed.
            var updated = DateTime.MinValue;
            if (!string.IsNullOrEmpty(trackedAsset.updated))
            {
                DateTime.TryParse(trackedAsset.updated, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out updated);
            }
            
            assetData.FillFromTracking(
                assetIdentifier,
                trackedAsset.sequenceNumber,
                trackedAsset.assetName,
                updated
            );

            var fileInfo = new ImportedFileInfo(
                trackedAsset.datasetId,
                trackedAsset.unityGUID,
                trackedAsset.path,
                trackedAsset.checksum,
                trackedAsset.timestamp,
                trackedAsset.metaFileChecksum,
                trackedAsset.metaFileTimestamp);

            return new ImportedAssetInfo(assetData, new[] { fileInfo });
        }
    }
}
