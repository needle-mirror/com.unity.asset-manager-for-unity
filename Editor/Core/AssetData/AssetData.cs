using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    [Serializable]
    class AssetData : BaseAssetData
    {
        static readonly int s_MaxThumbnailSize = 180;

        [SerializeField]
        AssetIdentifier m_Identifier;

        [SerializeField]
        int m_SequenceNumber;

        [SerializeField]
        int m_ParentSequenceNumber;

        [SerializeField]
        string m_Changelog;

        [SerializeField]
        string m_Name;

        [SerializeField]
        AssetType m_AssetType;

        [SerializeField]
        string m_Status;

        [SerializeField]
        string m_StatusFlowId;

        [SerializeField]
        string m_Description;

        [SerializeField]
        long m_Created;

        [SerializeField]
        long m_Updated;

        [SerializeField]
        string m_CreatedBy;

        [SerializeField]
        string m_UpdatedBy;

        [SerializeField]
        string m_PreviewFilePath;

        [SerializeField]
        bool m_IsFrozen;

        [SerializeField]
        List<string> m_Tags;

        [SerializeField]
        List<AssetIdentifier> m_Dependencies = new();

        [SerializeField]
        string m_ThumbnailUrl;

        [SerializeField]
        bool m_ThumbnailProcessed; // Flag to avoid multiple requests for assets with no thumbnail

        [SerializeField]
        bool m_DatasetProcessed; // Flag to avoid multiple requests for assets with no primary extension

        [SerializeReference]
        List<BaseAssetData> m_Versions = new();

        [SerializeReference]
        List<AssetLabel> m_Labels = new();

        [SerializeReference]
        List<HistoryChangeEntry> m_UpdateHistoryChanges;

        Task<Uri> m_GetPreviewStatusTask;
        Task m_AssetDataAttributesTask;
        Task m_DatasetTask;
        Task m_RefreshPropertiesTask;
        Task m_RefreshDependenciesTask;
        Task m_RefreshVersionsTask;
        Task m_RefreshUpdateHistoryTask;
        Task m_ThumbnailUrlTask;
        CachedTask m_LinkedProjectsTask;
        CachedTask m_LinkedCollectionsTask;
        CachedTask m_ReachableStatusNamesTask;

        IThumbnailDownloader m_ThumbnailDownloader;

        public override AssetIdentifier Identifier => m_Identifier;
        public override int SequenceNumber => m_SequenceNumber;
        public override int ParentSequenceNumber => m_ParentSequenceNumber;
        public override string Changelog => m_Changelog;
        public override string Name => m_Name;
        public override AssetType AssetType => m_AssetType;
        public override string Status => m_Status;
        public override string StatusFlowId => m_StatusFlowId;
        public override string Description => m_Description;
        public override DateTime? Created => new DateTime(m_Created, DateTimeKind.Utc);
        public override DateTime? Updated => new DateTime(m_Updated, DateTimeKind.Utc);
        public override string CreatedBy => m_CreatedBy;
        public override string UpdatedBy => m_UpdatedBy;
        public string PreviewFilePath => m_PreviewFilePath;
        public bool IsFrozen => m_IsFrozen;
        public override IEnumerable<string> Tags => m_Tags;

        public override IEnumerable<AssetIdentifier> Dependencies
        {
            get => m_Dependencies;
            internal set => m_Dependencies = value?.ToList() ?? new List<AssetIdentifier>();
        }

        public override IEnumerable<BaseAssetData> Versions => m_Versions;
        public override IReadOnlyList<HistoryChangeEntry> UpdateHistoryChanges => m_UpdateHistoryChanges ?? (IReadOnlyList<HistoryChangeEntry>)Array.Empty<HistoryChangeEntry>();

        IThumbnailDownloader ThumbnailDownloader =>
            m_ThumbnailDownloader ??= ServicesContainer.instance.Resolve<IThumbnailDownloader>();

        public override IEnumerable<AssetLabel> Labels => m_Labels;

        public AssetData() { }

#pragma warning disable S107 // Disabling the warning regarding too many parameters.
        public AssetData(AssetIdentifier assetIdentifier,
            int sequenceNumber,
            int parentSequenceNumber,
            string changelog,
            string name,
            AssetType assetType,
            string status,
            string statusFlowId,
            string description,
            DateTime created,
            DateTime updated,
            string createdBy,
            string updatedBy,
            string previewFilePath,
            bool isFrozen,
            IEnumerable<string> tags,
            IEnumerable<AssetLabel> labels = null,
            IEnumerable<ProjectIdentifier> linkedProjects = null,
            IEnumerable<CollectionIdentifier> linkedCollections = null)
        {
            m_Identifier = assetIdentifier;
            m_SequenceNumber = sequenceNumber;
            m_ParentSequenceNumber = parentSequenceNumber;
            m_Changelog = changelog;
            m_Name = name;
            m_AssetType = assetType;
            m_Status = status;
            m_StatusFlowId = statusFlowId;
            m_Description = description;
            m_Created = created.Ticks;
            m_Updated = updated.Ticks;
            m_CreatedBy = createdBy;
            m_UpdatedBy = updatedBy;
            m_PreviewFilePath = previewFilePath;
            m_IsFrozen = isFrozen;
            m_Tags = tags?.ToList() ?? new List<string>();
            m_Labels = labels?.ToList() ?? new List<AssetLabel>();
            m_LinkedProjects = linkedProjects?.ToList() ?? new List<ProjectIdentifier>();
            m_LinkedCollections = linkedCollections?.ToList() ?? new List<CollectionIdentifier>();
        }
#pragma warning restore S107

#pragma warning disable S107 // Disabling the warning regarding too many parameters.

        // Used when de-serialized from version 0.0 to fill data not in the IAsset
        public void FillFromPersistenceLegacy(IEnumerable<AssetIdentifier> dependencyAssets,
            string thumbnailUrl,
            IEnumerable<BaseAssetDataFile> sourceFiles,
            BaseAssetDataFile primarySourceFile)
        {
            m_Dependencies = dependencyAssets?.ToList();
            m_ThumbnailUrl = thumbnailUrl;

            var sourceAssetDataFiles = sourceFiles?.ToList();

            // Add a default dataset for the source files. Add a system tag to identify it is manually added. (NotSynced)
            m_Datasets = new List<AssetDataset>
            {
                new(k_Source, new List<string> {k_Source, k_NotSynced}, sourceAssetDataFiles)
            };

            m_PrimarySourceFile = primarySourceFile;
        }
#pragma warning restore S107

#pragma warning disable S107 // Disabling the warning regarding too many parameters.

        // Used when de-serialized from version 1.0
        public void FillFromPersistence(AssetIdentifier assetIdentifier,
            int sequenceNumber,
            int parentSequenceNumber,
            string changelog,
            string name,
            AssetType assetType,
            string status,
            string statusFlowId,
            string description,
            DateTime created,
            DateTime updated,
            string createdBy,
            string updatedBy,
            string previewFilePath,
            bool isFrozen,
            IEnumerable<string> tags,
            IEnumerable<AssetDataFile> sourceFiles,
            IEnumerable<AssetIdentifier> dependencies,
            IEnumerable<IMetadata> metadata)
        {
            var sourceAssetDataFiles = sourceFiles?.Cast<BaseAssetDataFile>().ToList();

            // Add a default dataset for the source files. Add a system tag to identify it is manually added. (NotSynced)
            var datasets = new List<AssetDataset>
            {
                new(k_Source, new List<string> {k_Source, k_NotSynced}, sourceAssetDataFiles)
            };

            FillFromPersistence(assetIdentifier, sequenceNumber, parentSequenceNumber, changelog, name, assetType, status, statusFlowId, description, created, updated, createdBy, updatedBy, previewFilePath, isFrozen, tags, datasets, dependencies, metadata);
        }
#pragma warning restore S107

#pragma warning disable S107 // Disabling the warning regarding too many parameters.

        // Used when de-serialized from version 2.0 and 3.0
        public void FillFromPersistence(AssetIdentifier assetIdentifier,
            int sequenceNumber,
            int parentSequenceNumber,
            string changelog,
            string name,
            AssetType assetType,
            string status,
            string statusFlowId,
            string description,
            DateTime created,
            DateTime updated,
            string createdBy,
            string updatedBy,
            string previewFilePath,
            bool isFrozen,
            IEnumerable<string> tags,
            IEnumerable<AssetDataset> datasets,
            IEnumerable<AssetIdentifier> dependencies,
            IEnumerable<IMetadata> metadata)
        {
            m_Identifier = assetIdentifier;
            m_SequenceNumber = sequenceNumber;
            m_ParentSequenceNumber = parentSequenceNumber;
            m_Changelog = changelog;
            m_Name = name;
            m_AssetType = assetType;
            m_Status = status;
            m_StatusFlowId = statusFlowId;
            m_Description = description;
            m_Created = created.Ticks;
            m_Updated = updated.Ticks;
            m_CreatedBy = createdBy;
            m_UpdatedBy = updatedBy;
            m_PreviewFilePath = previewFilePath;
            m_IsFrozen = isFrozen;
            m_Tags = tags.ToList();

            m_Dependencies = dependencies?.ToList();
            m_Metadata = new MetadataContainer(metadata);

            m_Datasets = datasets.ToList();

            ResolvePrimaryExtension();
        }
#pragma warning restore S107

        /// <summary>
        /// Fills AssetData with only the essential fields stored in tracking files.
        /// This method is used when reading from per-Unity-file tracking format (V4+),
        /// where only minimal tracking data is stored on disk.
        ///
        /// Additional fields (status, dependencies, metadata, etc.) are not stored in tracking files
        /// and will be populated from the UI cache when needed.
        /// </summary>
        /// <param name="assetIdentifier">The asset identifier</param>
        /// <param name="sequenceNumber">The sequence number for staleness detection</param>
        /// <param name="name">The asset name</param>
        /// <param name="updated">The last updated timestamp</param>
        public void FillFromTracking(AssetIdentifier assetIdentifier, int sequenceNumber, string name, DateTime updated)
        {
            m_Identifier = assetIdentifier;
            m_SequenceNumber = sequenceNumber;
            m_Name = name;
            m_Updated = updated.Ticks;

            // All other fields remain at default/empty values and will be populated from UI cache
            m_ParentSequenceNumber = 0;
            m_Changelog = string.Empty;
            m_AssetType = AssetType.Other;
            m_Status = string.Empty;
            m_StatusFlowId = string.Empty;
            m_Description = string.Empty;
            m_Created = DateTime.MinValue.Ticks;
            m_CreatedBy = string.Empty;
            m_UpdatedBy = string.Empty;
            m_PreviewFilePath = string.Empty;
            m_IsFrozen = false;
            m_Tags = new List<string>();
            m_Dependencies = new List<AssetIdentifier>();
            m_Metadata = new MetadataContainer();
            m_Datasets = new List<AssetDataset>();

            ResolvePrimaryExtension();
        }

        /// <summary>
        /// Repoints this asset at another project of the same organization, after an
        /// organization-wide lookup revealed the tracked project is no longer reachable.
        /// </summary>
        /// <remarks>
        /// Mutating the identifier in place is only safe because <see cref="TrackedAssetIdentifier"/>
        /// does not compare the project: an <see cref="AssetData"/> is shared by reference between
        /// the tracked map and the asset data repository, both keyed by that type, so changing a
        /// field that took part in the key would strand the entry in the wrong hash bucket.
        /// </remarks>
        internal void UpdateProjectId(string projectId)
        {
            if (m_Identifier == null || string.IsNullOrEmpty(projectId) || m_Identifier.ProjectId == projectId)
            {
                return;
            }

            m_Identifier = m_Identifier.WithProjectId(projectId);

            TaskUtils.TrackException(RefreshLinkedProjectsAsync());
        }

        void FillFromOther(AssetData other)
        {
            if (other == null || ReferenceEquals(this, other))
            {
                // The repository can hand back this very instance (see AssetDataManager.GetAssetAsync,
                // which returns the entry it already holds while its cache entry is fresh). Merging an
                // asset into itself has nothing to contribute and only risks self-assignment damage.
                return;
            }

            m_Identifier = other.Identifier;
            m_SequenceNumber = other.SequenceNumber;
            m_ParentSequenceNumber = other.ParentSequenceNumber;
            m_Changelog = other.Changelog;
            m_Name = other.Name;
            m_AssetType = other.AssetType;
            m_Status = other.Status;
            m_StatusFlowId = other.StatusFlowId;
            m_Description = other.Description;
            m_Created = other.Created?.Ticks ?? 0;
            m_Updated = other.Updated?.Ticks ?? 0;
            m_CreatedBy = other.CreatedBy;
            m_UpdatedBy = other.UpdatedBy;
            m_PreviewFilePath = other.PreviewFilePath;
            m_IsFrozen = other.IsFrozen;
            m_Tags = other.Tags?.ToList() ?? new List<string>();
            m_Metadata = new MetadataContainer(other.m_Metadata?.ToList() ?? new List<IMetadata>());
            m_Labels = other.Labels.ToList();
            m_LinkedProjects = other.LinkedProjects.ToList();

            // Focus on copying the primary datasets.
            foreach (var dataset in other.Datasets)
            {
                _ = AssetDataset.k_PrimaryDatasetSystemTags.FirstOrDefault(x => TryCopyDataset(dataset, x));
            }
        }

        /// <summary>
        /// Populates display-oriented fields from an AssetDataCache entry.
        /// Can override tracking file data with fresher cached data.
        /// Note: This method directly sets backing fields to avoid triggering multiple events.
        /// </summary>
        public void PopulateFromAssetDataCache(AssetDataCacheEntry entry)
        {
            if (entry == null)
                return;

            // Core properties - only override if cache has non-empty values
            if (!string.IsNullOrEmpty(entry.name))
                m_Name = entry.name;

            if (!string.IsNullOrEmpty(entry.description))
                m_Description = entry.description;

            if (!string.IsNullOrEmpty(entry.status))
                m_Status = entry.status;

            if (!string.IsNullOrEmpty(entry.statusFlowId))
                m_StatusFlowId = entry.statusFlowId;

            if (!string.IsNullOrEmpty(entry.changelog))
                m_Changelog = entry.changelog;

            // Asset type is not stored in the minimal tracking file (FillFromTracking defaults it
            // to Other); it is persisted in the cache and must be restored here. Only override when
            // the cache holds a concrete type so we never clobber a good value with a stale default.
            if (entry.assetType != AssetType.Other)
                m_AssetType = entry.assetType;

            if (!string.IsNullOrEmpty(entry.createdBy))
                m_CreatedBy = entry.createdBy;

            if (!string.IsNullOrEmpty(entry.updatedBy))
                m_UpdatedBy = entry.updatedBy;

            if (!string.IsNullOrEmpty(entry.previewFilePath))
                m_PreviewFilePath = entry.previewFilePath;

            m_IsFrozen = entry.isFrozen;

            if (entry.tags != null && entry.tags.Count > 0)
                m_Tags = entry.tags.ToList();

            if (!string.IsNullOrEmpty(entry.created) && DateTime.TryParse(entry.created, null, System.Globalization.DateTimeStyles.RoundtripKind, out var created))
                m_Created = created.Ticks;

            if (!string.IsNullOrEmpty(entry.updated) && DateTime.TryParse(entry.updated, null, System.Globalization.DateTimeStyles.RoundtripKind, out var updated))
                m_Updated = updated.Ticks;

            // Linked projects (set directly to backing field to avoid event)
            if (entry.linkedProjects != null && entry.linkedProjects.Count > 0)
            {
                m_LinkedProjects = entry.linkedProjects
                    .Where(p => !string.IsNullOrEmpty(p.projectId))
                    .Select(p => new ProjectIdentifier(p.organizationId, p.projectId))
                    .ToList();
            }

            // Linked collections (set directly to backing field to avoid event)
            if (entry.linkedCollections != null && entry.linkedCollections.Count > 0)
            {
                m_LinkedCollections = entry.linkedCollections
                    .Where(c => !string.IsNullOrEmpty(c.collectionPath))
                    .Select(c => new CollectionIdentifier(
                        new ProjectIdentifier(c.organizationId, c.projectId),
                        c.collectionPath))
                    .ToList();
            }

            // Metadata - populate from cache if present
            var metadata = AssetDataCacheConverter.ConvertMetadataFromCache(entry.metadata);
            if (metadata.Any())
                SetMetadata(metadata);

            // Populate datasets from cache (dataset definitions are per-asset data stored in the cache,
            // not in per-file tracking files which only reference datasets by ID)
            if (entry.datasets != null && entry.datasets.Count > 0)
            {
                var filesByPath = entry.files?.ToDictionary(f => f.path, f => f)
                    ?? new Dictionary<string, AssetDataCacheFile>();

                m_Datasets = entry.datasets
                    .Where(d => !string.IsNullOrEmpty(d.id))
                    .Select(d =>
                    {
                        var datasetFiles = d.fileKeys?
                            .Select(key => filesByPath.GetValueOrDefault(key))
                            .Where(f => f != null)
                            .Select(f => (BaseAssetDataFile)new AssetDataFile(
                                f.path, f.extension, null, f.description, f.tags?.ToList(), f.fileSize, f.available))
                            .ToList();

                        return new AssetDataset(d.id, d.name, d.systemTags, datasetFiles);
                    })
                    .ToList();

                ResolvePrimaryExtension();
            }

            // Populate parentSequenceNumber if available in cache
            if (entry.parentSequenceNumber != 0)
                m_ParentSequenceNumber = entry.parentSequenceNumber;

            // Update identifier with versionLabel and libraryId if available
            if (m_Identifier != null && (!string.IsNullOrEmpty(entry.versionLabel) || !string.IsNullOrEmpty(entry.libraryId)))
            {
                var newIdentifier = new AssetIdentifier(
                    m_Identifier.OrganizationId,
                    m_Identifier.ProjectId,
                    m_Identifier.AssetId,
                    m_Identifier.Version,
                    entry.versionLabel ?? m_Identifier.VersionLabel);

                // Set libraryId via internal setter
                if (!string.IsNullOrEmpty(entry.libraryId))
                    newIdentifier.LibraryId = entry.libraryId;
                else if (!string.IsNullOrEmpty(m_Identifier.LibraryId))
                    newIdentifier.LibraryId = m_Identifier.LibraryId;

                m_Identifier = newIdentifier;
            }

            // Populate dependencies if available in cache
            if (entry.dependencyAssets != null && entry.dependencyAssets.Count > 0)
            {
                m_Dependencies = entry.dependencyAssets
                    .Where(d => !string.IsNullOrEmpty(d.assetId))
                    .Select(d => new AssetIdentifier(
                        d.organizationId,
                        d.projectId,
                        d.assetId,
                        d.versionId,
                        d.versionLabel))
                    .ToList();

                // Set libraryId for each dependency
                for (int i = 0; i < m_Dependencies.Count && i < entry.dependencyAssets.Count; i++)
                {
                    if (!string.IsNullOrEmpty(entry.dependencyAssets[i].libraryId))
                    {
                        m_Dependencies[i].LibraryId = entry.dependencyAssets[i].libraryId;
                    }
                }
            }
        }

        /// <summary>
        /// Refreshes asset attributes and notifies subscribers after cache data has been populated.
        /// Call this after <see cref="IAssetDataCacheManager.PopulateFromCache"/> to update import status and UI.
        /// </summary>
        /// <param name="token">Cancellation token to abort the refresh operation.</param>
        public async Task NotifyCachePopulatedAsync(CancellationToken token = default)
        {
            await RefreshAssetDataAttributesAsync(token);
            InvokeEvent(AssetDataEventType.PropertiesChanged);
            InvokeEvent(AssetDataEventType.AssetDataAttributesChanged);
        }

        public override async Task GetThumbnailAsync(CancellationToken token = default)
        {
            // Because a AssetData is tied to a version, and preview modification creates a new version,
            // we can assume that the thumbnail is always the same.
            if (Thumbnail != null || m_ThumbnailProcessed)
            {
                return;
            }

            // Look inside the cache before making any request
            var cachedThumbnail = ThumbnailDownloader.GetCachedThumbnail(Identifier);
            if (cachedThumbnail != null)
            {
                m_ThumbnailProcessed = true;
                Thumbnail = cachedThumbnail;
                return;
            }

            m_ThumbnailUrlTask ??= GetThumbnailUrlAsync(token);

            try
            {
                await m_ThumbnailUrlTask;
            }
            finally
            {
                m_ThumbnailUrlTask = null;
            }

            ThumbnailDownloader.DownloadThumbnail(Identifier, m_ThumbnailUrl,
                (_, texture) =>
                {
                    m_ThumbnailProcessed = true;
                    Thumbnail = texture;
                });
        }

        async Task GetThumbnailUrlAsync(CancellationToken token)
        {
            var assetsSdkProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();

            m_GetPreviewStatusTask ??= assetsSdkProvider.GetPreviewUrlAsync(this, s_MaxThumbnailSize, token);

            Uri previewFileUrl = null;

            try
            {
                previewFileUrl = await m_GetPreviewStatusTask;
            }
            catch (HttpRequestException)
            {
                // Ignore unreachable host
            }
            catch (NotFoundException)
            {
                // Ignore if the Asset is not found
            }
            catch (ForbiddenException)
            {
                // Ignore if the Asset is unavailable
            }
            finally
            {
                m_GetPreviewStatusTask = null;
            }

            m_ThumbnailUrl = previewFileUrl?.ToString() ?? string.Empty;
        }

        public override async Task RefreshAssetDataAttributesAsync(CancellationToken token = default)
        {
            var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
            var isInProject = assetDataManager.IsInProject(Identifier);

            if (AssetDataAttributeCollection != null && AssetDataAttributeCollection.HasAttribute<ImportAttribute>())
            {
                // Check if the asset is still in the project anymore, if not clear the import status.
                if (!isInProject)
                {
                    AssetDataAttributeCollection = null;
                }
            }

            var unityConnectProxy = ServicesContainer.instance.Resolve<IUnityConnectProxy>();
            if (m_AssetDataAttributesTask == null && unityConnectProxy.AreCloudServicesReachable && isInProject)
            {
                m_AssetDataAttributesTask = GetDatasetAttributesInternalAsync(token);
            }

            if (m_AssetDataAttributesTask != null)
            {
                try
                {
                    await m_AssetDataAttributesTask;
                }
                catch (HttpRequestException)
                {
                    // Ignore unreachable host
                }
                catch (ForbiddenException)
                {
                    // Ignore if the Asset is unavailable
                }
                catch (NotFoundException)
                {
                    // Ignore if the Asset is not found
                }
                finally
                {
                    m_AssetDataAttributesTask = null;
                }
            }
        }

        async Task GetDatasetAttributesInternalAsync(CancellationToken token = default)
        {
            var assetsSdkProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
            var results = await assetsSdkProvider.GatherImportStatusesAsync(new[] {this}, token);

            if (results.TryGetValue(Identifier, out var status))
            {
                AssetDataAttributeCollection = new AssetDataAttributeCollection(new ImportAttribute(status));
            }
            else
            {
                ResetAssetDataAttributes();
            }
        }

        // Mirrors the early exit of ResolveDatasetsAsync; when it is true, resolving the datasets is a no-op
        // and the file list can be trusted to be complete.
        public override bool AreDatasetsResolved => m_DatasetProcessed || !string.IsNullOrEmpty(PrimaryExtension);

        public override async Task ResolveDatasetsAsync(CancellationToken token = default)
        {
            // Because an AssetData is tied to a version, and files modification creates a new version,
            // we can assume that the primary extension is always the same.
            if (AreDatasetsResolved)
                return;

            // Wait for the refresh of properties as dataset info will be bundled
            if (m_RefreshPropertiesTask != null)
            {
                await m_RefreshPropertiesTask;
            }

            m_DatasetTask ??= ResolveDatasetInternalAsync(token);

            try
            {
                await m_DatasetTask;
            }
            catch (HttpRequestException)
            {
                // Ignore unreachable host
            }
            catch (ForbiddenException)
            {
                // Ignore if the Asset is unavailable
            }
            catch (NotFoundException)
            {
                // Ignore if the Asset is not found
            }
            finally
            {
                m_DatasetTask = null;
            }
        }

        async Task ResolveDatasetInternalAsync(CancellationToken token = default)
        {
            var assetsProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();

            var sourceDataset = Datasets.FirstOrDefault(d => d.IsSource);

            if (sourceDataset == null)
            {
                // Expected when asset was loaded from tracking file only (e.g. VCS-synced); dataset info
                // comes from cloud/cache. Use placeholder until cache refresh or cloud fetch populates it.
                Utilities.DevLog($"Source dataset not set for asset {Name}; using placeholder until cache/cloud data is available");

                sourceDataset = new AssetDataset(k_Source, new List<string> {k_Source, k_NotSynced}, null);
                Datasets = new List<AssetDataset> {sourceDataset};
            }

            var tasks = Datasets.Select(dataset => dataset.GetFilesAsync(assetsProvider, Identifier, token));
            await Task.WhenAll(tasks);

            token.ThrowIfCancellationRequested();

            ResolvePrimaryExtension();

            // Mark the datasets resolved before notifying, so a listener that refreshes off this event
            // sees AreDatasetsResolved as true. Otherwise an asset that resolves to no importable files
            // still reports itself as potentially importable to that refresh, and nothing corrects it
            // afterwards.
            m_DatasetProcessed = true;

            InvokeEvent(AssetDataEventType.FilesChanged);
        }

        public override async Task RefreshPropertiesAsync(CancellationToken token = default)
        {
            m_RefreshPropertiesTask ??= RefreshPropertiesInternalAsync(token);
            try
            {
                await m_RefreshPropertiesTask;
            }
            catch (HttpRequestException)
            {
                // Ignore unreachable host
            }
            catch (ForbiddenException)
            {
                // Ignore if the Asset is unavailable
            }
            catch (NotFoundException)
            {
                // Ignore if the Asset is not found
            }
            finally
            {
                m_RefreshPropertiesTask = null;
            }
        }

        async Task RefreshPropertiesInternalAsync(CancellationToken token = default)
        {
            var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
            var updatedAsset = await assetDataManager.GetAssetAsync(Identifier, token);

            if (updatedAsset == null)
            {
                return;
            }

            FillFromOther(updatedAsset);

            InvokeEvent(AssetDataEventType.PropertiesChanged);
        }

        public override async Task RefreshDependenciesAsync(CancellationToken token = default)
        {
            m_RefreshDependenciesTask ??= RefreshDependenciesInternalAsync(token);
            try
            {
                await m_RefreshDependenciesTask;
            }
            catch (HttpRequestException)
            {
                // Ignore unreachable host
            }
            catch (ForbiddenException)
            {
                // Ignore if the Asset is unavailable
            }
            catch (NotFoundException)
            {
                // Ignore if the Asset is not found
            }
            finally
            {
                m_RefreshDependenciesTask = null;
            }
        }

        async Task RefreshDependenciesInternalAsync(CancellationToken token)
        {
            var dependencies = new List<AssetIdentifier>();
            await foreach (var dependency in AssetDataDependencyHelper.LoadDependenciesAsync(this, token))
            {
                dependencies.Add(dependency);
            }

            m_Dependencies = dependencies;

            InvokeEvent(AssetDataEventType.DependenciesChanged);
        }

        public override async Task RefreshVersionsAsync(CancellationToken token = default)
        {
            m_RefreshVersionsTask ??= RefreshVersionsInternalAsync(token);
            try
            {
                await m_RefreshVersionsTask;
            }
            catch (HttpRequestException)
            {
                // Ignore unreachable host
            }
            catch (ForbiddenException)
            {
                // Ignore if the Asset is unavailable
            }
            catch (NotFoundException)
            {
                // Ignore if the Asset is not found
            }
            finally
            {
                m_RefreshVersionsTask = null;
            }
        }

        async Task RefreshVersionsInternalAsync(CancellationToken token)
        {
            var assetsSdkProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
            var versions = new List<BaseAssetData>();
            await foreach (var assetData in assetsSdkProvider.ListVersionInDescendingOrderAsync(m_Identifier, token))
            {
                versions.Add(assetData);
                assetData.m_Versions = versions;
            }

            m_Versions = versions;
        }

        public override async Task RefreshUpdateHistoryAsync(CancellationToken token = default)
        {
            m_RefreshUpdateHistoryTask ??= RefreshUpdateHistoryInternalAsync(token);
            try
            {
                await m_RefreshUpdateHistoryTask;
            }
            catch (HttpRequestException)
            {
                // Ignore unreachable host
            }
            catch (ForbiddenException)
            {
                // Ignore if the Asset is unavailable
            }
            catch (NotFoundException)
            {
                // Ignore if the Asset is not found
            }
            finally
            {
                m_RefreshUpdateHistoryTask = null;
            }
        }

        async Task RefreshUpdateHistoryInternalAsync(CancellationToken token)
        {
            var assetsSdkProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
            var list = new List<AssetUpdateHistorySnapshot>();
            await foreach (var entry in assetsSdkProvider.GetUpdateHistoryAsync(m_Identifier, token))
            {
                var tags = entry.Tags != null ? new List<string>(entry.Tags) : (List<string>)null;
                var metadata = MapHistoryMetadataToStrings(entry.Metadata);
                list.Add(new AssetUpdateHistorySnapshot(
                    entry.SequenceNumber,
                    entry.Updated,
                    entry.UpdatedBy.ToString(),
                    entry.Name,
                    entry.Description,
                    tags,
                    metadata));
            }
            // SDK returns history in descending order (newest first); diff helper expects chronological order (oldest first).
            list.Reverse();
            m_UpdateHistoryChanges = HistoryDiffHelper.FromSnapshots(list);
        }

        static Dictionary<string, string> MapHistoryMetadataToStrings<T>(IReadOnlyDictionary<string, T> metadata)
        {
            if (metadata == null || metadata.Count == 0)
                return new Dictionary<string, string>();
            var result = new Dictionary<string, string>();
            foreach (var kvp in metadata)
            {
                try
                {
                    result[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
                }
                catch
                {
                    result[kvp.Key] = string.Empty;
                }
            }
            return result;
        }

        public override async Task RefreshLinkedProjectsAsync(CancellationToken token = default)
        {
            m_LinkedProjectsTask ??= new CachedTask(RefreshLinkedProjectsInternalAsync);

            try
            {
                await m_LinkedProjectsTask.RunAsync(token, 25);
            }
            catch (HttpRequestException)
            {
                // Ignore unreachable host
            }
            catch (ForbiddenException)
            {
                // Ignore if the Asset is unavailable
            }
            catch (NotFoundException)
            {
                // Ignore if the Asset is not found
            }
        }

        async Task RefreshLinkedProjectsInternalAsync(CancellationToken token = default)
        {
            var assetsSdkProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
            LinkedProjects = await assetsSdkProvider.GetLinkedProjectsAsync(this, token);
        }

        public override async Task RefreshLinkedCollectionsAsync(CancellationToken token = default)
        {
            m_LinkedCollectionsTask ??= new CachedTask(RefreshLinkedCollectionsInternalAsync);

            try
            {
                await m_LinkedCollectionsTask.RunAsync(token, 25);
            }
            catch (HttpRequestException)
            {
                // Ignore unreachable host
            }
            catch (ForbiddenException)
            {
                // Ignore if the Asset is unavailable
            }
            catch (NotFoundException)
            {
                // Ignore if the Asset is not found
            }
        }

        async Task RefreshLinkedCollectionsInternalAsync(CancellationToken token = default)
        {
            if (Identifier.IsAssetFromLibrary()) // Currently, we cannot fetch linked collections for library assets
                return;

            var assetsSdkProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
            LinkedCollections = await assetsSdkProvider.GetLinkedCollectionsAsync(this, token);
        }

        public override async Task RefreshReachableStatusNamesAsync(CancellationToken token = default)
        {
            m_ReachableStatusNamesTask ??= new CachedTask(RefreshReachableStatusNamesInternalAsync);

            try
            {
                await m_ReachableStatusNamesTask.RunAsync(token, 25);
            }
            catch (HttpRequestException)
            {
                // Ignore unreachable host
            }
            catch (ForbiddenException)
            {
                // Ignore if the Asset is unavailable
            }
            catch (NotFoundException)
            {
                // Ignore if the Asset is not found
            }
        }

        async Task RefreshReachableStatusNamesInternalAsync(CancellationToken token = default)
        {
            var assetsSdkProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
            ReachableStatusNames = await assetsSdkProvider.GetReachableStatusNamesAsync(Identifier, token);
        }

        bool TryCopyDataset(AssetDataset dataset, string targetSystemLabel)
        {
            if (!dataset.SystemTags.Contains(targetSystemLabel)) return false;

            // Find a matching dataset by id (Persistence >= V4)
            var existingDataset = m_Datasets.Find(x => x.Id == dataset.Id);
            if (existingDataset != null)
            {
                existingDataset.Copy(dataset);

                return true;
            }

            // Fallback, find a matching dataset by system label (Persistence <= V3)
            existingDataset = m_Datasets.Find(x => x.SystemTags.Contains(targetSystemLabel));
            if (existingDataset != null)
            {
                existingDataset.Copy(dataset);
            }
            else
            {
                m_Datasets.Add(dataset);
            }

            return true;
        }
    }
}
