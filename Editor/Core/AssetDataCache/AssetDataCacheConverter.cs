using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Converts between AssetData and AssetDataCacheEntry.
    ///
    /// This converter follows the same patterns as PersistenceV3 serialization
    /// to ensure data format compatibility between tracking files and the AssetDataCache.
    /// </summary>
    static class AssetDataCacheConverter
    {
        /// <summary>
        /// Creates an AssetDataCacheEntry from an AssetData object.
        /// Mirrors the serialization logic in PersistenceV3.SerializeEntry.
        /// </summary>
        public static AssetDataCacheEntry FromAssetData(AssetData assetData)
        {
            if (assetData == null)
                return null;

            var entry = new AssetDataCacheEntry
            {
                // Cache metadata
                cacheVersion = AssetDataCacheEntry.CurrentCacheVersion,
                cachedAt = DateTime.UtcNow.ToString("o"),

                // Asset identification (same as PersistenceV3)
                organizationId = assetData.Identifier?.OrganizationId,
                projectId = assetData.Identifier?.ProjectId,
                assetId = assetData.Identifier?.AssetId,
                versionId = assetData.Identifier?.Version,
                versionLabel = assetData.Identifier?.VersionLabel,
                libraryId = assetData.Identifier?.LibraryId,

                // Version info (same fields as TrackedAssetVersionPersisted)
                parentSequenceNumber = assetData.ParentSequenceNumber,
                name = assetData.Name,
                changelog = assetData.Changelog,
                assetType = assetData.AssetType,
                status = assetData.Status,
                statusFlowId = assetData.StatusFlowId,
                description = assetData.Description,
                created = assetData.Created?.ToString("o"),
                createdBy = assetData.CreatedBy,
                updatedBy = assetData.UpdatedBy,
                previewFilePath = assetData.PreviewFilePath,
                isFrozen = assetData.IsFrozen,
                tags = assetData.Tags?.ToList() ?? new List<string>()
            };

            // Convert datasets (same structure as TrackedDatasetPersisted)
            if (assetData.Datasets != null)
            {
                entry.datasets = assetData.Datasets
                    .Select(d => new AssetDataCacheDataset(
                        d.Id,
                        d.Name,
                        d.SystemTags?.ToList(),
                        d.Files?.Select(f => f.Path).ToList()))
                    .ToList();

                // Convert files per dataset (same structure as TrackedFilePersisted, UI fields only)
                var files = new List<AssetDataCacheFile>();
                foreach (var dataset in assetData.Datasets)
                {
                    if (dataset.Files == null)
                        continue;

                    files.AddRange(dataset.Files.Select(f => new AssetDataCacheFile(
                        dataset.Id,
                        f.Path,
                        f.Extension,
                        f.Available,
                        f.Description,
                        f.FileSize,
                        f.Tags?.ToList())));
                }
                entry.files = files;
            }

            // Convert metadata (same structure as TrackedMetadataPersisted)
            entry.metadata = ConvertMetadata(assetData.Metadata);

            // Convert linked data (new, not in PersistenceV3)
            if (assetData.LinkedProjects != null)
            {
                entry.linkedProjects = assetData.LinkedProjects
                    .Select(p => new AssetDataCacheLinkedProject(p.OrganizationId, p.ProjectId))
                    .ToList();
            }

            if (assetData.LinkedCollections != null)
            {
                entry.linkedCollections = assetData.LinkedCollections
                    .Select(c => new AssetDataCacheLinkedCollection(
                        c.ProjectIdentifier?.OrganizationId,
                        c.ProjectIdentifier?.ProjectId,
                        c.CollectionPath))
                    .ToList();
            }

            if (assetData.Dependencies != null)
            {
                entry.dependencyAssets = assetData.Dependencies
                    .Select(d => new AssetDataCacheDependency(
                        d.OrganizationId,
                        d.ProjectId,
                        d.AssetId,
                        d.Version,
                        d.VersionLabel,
                        d.LibraryId))
                    .ToList();
            }

            return entry;
        }

        /// <summary>
        /// Updates linked projects in a cache entry.
        /// Replaces existing linked projects with the provided list.
        /// </summary>
        /// <param name="entry">The cache entry to update.</param>
        /// <param name="linkedProjects">The linked project identifiers to store.</param>
        public static void UpdateLinkedProjects(AssetDataCacheEntry entry, IEnumerable<ProjectIdentifier> linkedProjects)
        {
            if (entry == null)
                return;

            entry.linkedProjects = linkedProjects?
                .Select(p => new AssetDataCacheLinkedProject(p.OrganizationId, p.ProjectId))
                .ToList() ?? new List<AssetDataCacheLinkedProject>();
        }

        /// <summary>
        /// Updates linked collections in a cache entry.
        /// Replaces existing linked collections with the provided list.
        /// </summary>
        /// <param name="entry">The cache entry to update.</param>
        /// <param name="linkedCollections">The linked collection identifiers to store.</param>
        public static void UpdateLinkedCollections(AssetDataCacheEntry entry, IEnumerable<CollectionIdentifier> linkedCollections)
        {
            if (entry == null)
                return;

            entry.linkedCollections = linkedCollections?
                .Select(c => new AssetDataCacheLinkedCollection(
                    c.ProjectIdentifier?.OrganizationId,
                    c.ProjectIdentifier?.ProjectId,
                    c.CollectionPath))
                .ToList() ?? new List<AssetDataCacheLinkedCollection>();
        }

        /// <summary>
        /// Populates an AssetData object with all cached asset data.
        /// For AssetData instances, this includes all UI-relevant fields (name, status, metadata, etc.).
        /// For other BaseAssetData types, only linked data is populated via public setters.
        /// </summary>
        /// <param name="assetData">The asset data to populate. Must not be null.</param>
        /// <param name="entry">The cache entry to read from. If null, no population occurs.</param>
        /// <returns>True if the asset data was populated, false otherwise.</returns>
        public static bool PopulateFromCache(BaseAssetData assetData, AssetDataCacheEntry entry)
        {
            if (assetData == null || entry == null)
                return false;

            if (assetData is AssetData concreteAssetData)
            {
                concreteAssetData.PopulateFromAssetDataCache(entry);
                return true;
            }

            return PopulateLinkedData(assetData, entry);
        }

        /// <summary>
        /// Populates only linked projects and collections from a cache entry.
        /// Used as a fallback for non-AssetData types.
        /// </summary>
        static bool PopulateLinkedData(BaseAssetData assetData, AssetDataCacheEntry entry)
        {
            if (assetData == null || entry == null)
                return false;

            bool populated = false;

            // Populate linked projects if available in cache
            if (entry.linkedProjects != null && entry.linkedProjects.Count > 0)
            {
                var linkedProjects = entry.linkedProjects
                    .Where(p => !string.IsNullOrEmpty(p.projectId))
                    .Select(p => new ProjectIdentifier(p.organizationId, p.projectId))
                    .ToList();

                if (linkedProjects.Count > 0)
                {
                    assetData.LinkedProjects = linkedProjects;
                    populated = true;
                }
            }

            // Populate linked collections if available in cache
            if (entry.linkedCollections != null && entry.linkedCollections.Count > 0)
            {
                var linkedCollections = entry.linkedCollections
                    .Where(c => !string.IsNullOrEmpty(c.collectionPath))
                    .Select(c => new CollectionIdentifier(
                        new ProjectIdentifier(c.organizationId, c.projectId),
                        c.collectionPath))
                    .ToList();

                if (linkedCollections.Count > 0)
                {
                    assetData.LinkedCollections = linkedCollections;
                    populated = true;
                }
            }

            return populated;
        }

        /// <summary>
        /// Converts metadata from cache format back to IMetadata objects.
        /// This is the reverse of ConvertMetadata.
        /// </summary>
        public static IEnumerable<IMetadata> ConvertMetadataFromCache(AssetDataCacheMetadata cacheMetadata)
        {
            if (cacheMetadata == null)
                return Enumerable.Empty<IMetadata>();

            var metadata = new List<IMetadata>();

            // Text metadata
            if (cacheMetadata.textMedatatas != null)
            {
                foreach (var m in cacheMetadata.textMedatatas)
                {
                    if (!string.IsNullOrEmpty(m.key))
                        metadata.Add(new TextMetadata(m.key, m.displayName, m.value));
                }
            }

            // Boolean metadata
            if (cacheMetadata.booleanMedatatas != null)
            {
                foreach (var m in cacheMetadata.booleanMedatatas)
                {
                    if (!string.IsNullOrEmpty(m.key))
                        metadata.Add(new BooleanMetadata(m.key, m.displayName, m.value));
                }
            }

            // Number metadata
            if (cacheMetadata.numberMedatatas != null)
            {
                foreach (var m in cacheMetadata.numberMedatatas)
                {
                    if (!string.IsNullOrEmpty(m.key))
                        metadata.Add(new NumberMetadata(m.key, m.displayName, m.value));
                }
            }

            // URL metadata
            if (cacheMetadata.urlMedatatas != null)
            {
                foreach (var m in cacheMetadata.urlMedatatas)
                {
                    if (!string.IsNullOrEmpty(m.key))
                    {
                        Uri uri = null;
                        if (!string.IsNullOrEmpty(m.value.uri))
                            Uri.TryCreate(m.value.uri, UriKind.Absolute, out uri);

                        metadata.Add(new UrlMetadata(m.key, m.displayName, new UriEntry(uri, m.value.label)));
                    }
                }
            }

            // Timestamp metadata
            if (cacheMetadata.timestampMedatatas != null)
            {
                foreach (var m in cacheMetadata.timestampMedatatas)
                {
                    if (!string.IsNullOrEmpty(m.key))
                    {
                        if (DateTime.TryParse(m.value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dateTime))
                        {
                            metadata.Add(new TimestampMetadata(m.key, m.displayName, new DateTimeEntry(dateTime)));
                        }
                    }
                }
            }

            // User metadata
            if (cacheMetadata.userMedatatas != null)
            {
                foreach (var m in cacheMetadata.userMedatatas)
                {
                    if (!string.IsNullOrEmpty(m.key))
                        metadata.Add(new UserMetadata(m.key, m.displayName, m.value));
                }
            }

            // Single selection metadata
            if (cacheMetadata.singleSelectionMedatatas != null)
            {
                foreach (var m in cacheMetadata.singleSelectionMedatatas)
                {
                    if (!string.IsNullOrEmpty(m.key))
                        metadata.Add(new SingleSelectionMetadata(m.key, m.displayName, m.value));
                }
            }

            // Multi selection metadata
            if (cacheMetadata.multiSelectionMedatatas != null)
            {
                foreach (var m in cacheMetadata.multiSelectionMedatatas)
                {
                    if (!string.IsNullOrEmpty(m.key))
                        metadata.Add(new MultiSelectionMetadata(m.key, m.displayName, m.value?.ToList()));
                }
            }

            return metadata;
        }

        /// <summary>
        /// Converts metadata container to cache format.
        /// Mirrors PersistenceV3.Convert().
        /// </summary>
        static AssetDataCacheMetadata ConvertMetadata(IMetadataContainer metadataContainer)
        {
            var cacheMetadata = new AssetDataCacheMetadata();

            if (metadataContainer == null)
                return cacheMetadata;

            foreach (var metadata in metadataContainer)
            {
                switch (metadata)
                {
                    case TextMetadata textMetadata:
                        cacheMetadata.textMedatatas.Add(new AssetDataCacheStringMetadata(
                            textMetadata.FieldKey,
                            textMetadata.Name,
                            textMetadata.Value));
                        break;

                    case BooleanMetadata booleanMetadata:
                        cacheMetadata.booleanMedatatas.Add(new AssetDataCacheBooleanMetadata(
                            booleanMetadata.FieldKey,
                            booleanMetadata.Name,
                            booleanMetadata.Value));
                        break;

                    case NumberMetadata numberMetadata:
                        cacheMetadata.numberMedatatas.Add(new AssetDataCacheNumberMetadata(
                            numberMetadata.FieldKey,
                            numberMetadata.Name,
                            numberMetadata.Value));
                        break;

                    case UrlMetadata urlMetadata:
                        cacheMetadata.urlMedatatas.Add(new AssetDataCacheUrlMetadata(
                            urlMetadata.FieldKey,
                            urlMetadata.Name,
                            new AssetDataCacheUri(
                                urlMetadata.Value.Uri?.ToString() ?? string.Empty,
                                urlMetadata.Value.Label ?? string.Empty)));
                        break;

                    case TimestampMetadata timestampMetadata:
                        cacheMetadata.timestampMedatatas.Add(new AssetDataCacheStringMetadata(
                            timestampMetadata.FieldKey,
                            timestampMetadata.Name,
                            timestampMetadata.Value.DateTime.ToString("o")));
                        break;

                    case UserMetadata userMetadata:
                        cacheMetadata.userMedatatas.Add(new AssetDataCacheStringMetadata(
                            userMetadata.FieldKey,
                            userMetadata.Name,
                            userMetadata.Value));
                        break;

                    case SingleSelectionMetadata singleSelectionMetadata:
                        cacheMetadata.singleSelectionMedatatas.Add(new AssetDataCacheStringMetadata(
                            singleSelectionMetadata.FieldKey,
                            singleSelectionMetadata.Name,
                            singleSelectionMetadata.Value));
                        break;

                    case MultiSelectionMetadata multiSelectionMetadata:
                        cacheMetadata.multiSelectionMedatatas.Add(new AssetDataCacheStringListMetadata(
                            multiSelectionMetadata.FieldKey,
                            multiSelectionMetadata.Name,
                            multiSelectionMetadata.Value?.ToList()));
                        break;
                }
            }

            return cacheMetadata;
        }
    }
}
