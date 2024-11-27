using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// This is a class containing the information about an asset.
    /// </summary>
    sealed class AssetEntity : IAsset
    {
        readonly IAssetDataSource m_DataSource;

        internal ProjectDescriptor[] m_LinkedProjects = Array.Empty<ProjectDescriptor>();

        /// <inheritdoc />
        public AssetDescriptor Descriptor { get; }

        /// <inheritdoc />
        public AssetState State { get; set; }

        /// <inheritdoc />
        public int FrozenSequenceNumber { get; set; }

        /// <inheritdoc />
        public string Changelog { get; set; }

        /// <inheritdoc />
        public AssetVersion ParentVersion { get; set; }

        /// <inheritdoc />
        public int ParentFrozenSequenceNumber { get; set; }

        /// <inheritdoc />
        public ProjectDescriptor SourceProject { get; set; }

        /// <inheritdoc />
        public IEnumerable<ProjectDescriptor> LinkedProjects => m_LinkedProjects;

        /// <inheritdoc />
        public string Name { get; set; }

        /// <inheritdoc />
        public string Description { get; set; }

        /// <inheritdoc />
        public IEnumerable<string> Tags { get; set; }

        /// <inheritdoc />
        public IEnumerable<string> SystemTags { get; set; }

        /// <inheritdoc />
        public IEnumerable<LabelDescriptor> Labels { get; set; }

        /// <inheritdoc />
        public IEnumerable<LabelDescriptor> ArchivedLabels { get; set; }

        /// <inheritdoc />
        public AssetType Type { get; set; } = AssetType.Other;

        /// <inheritdoc />
        public IMetadataContainer Metadata => MetadataEntity;

        /// <inheritdoc />
        public IReadOnlyMetadataContainer SystemMetadata => SystemMetadataEntity;

        /// <inheritdoc />
        public string PreviewFile { get; set; }

        /// <inheritdoc />
        public FileDescriptor PreviewFileDescriptor { get; set; }

        /// <inheritdoc />
        public string Status { get; set; }

        /// <inheritdoc />
        public string StatusName { get; set; }

        /// <inheritdoc />
        public StatusFlowDescriptor StatusFlowDescriptor { get; set; }

        /// <inheritdoc />
        public AuthoringInfo AuthoringInfo { get; set; }

        internal Uri PreviewFileUrl { get; set; }
        internal MetadataContainerEntity MetadataEntity { get; }
        internal ReadOnlyMetadataContainerEntity SystemMetadataEntity { get; }

        internal AssetEntity(IAssetDataSource dataSource, AssetDescriptor assetDescriptor)
        {
            m_DataSource = dataSource;
            Descriptor = assetDescriptor;

            MetadataEntity = new MetadataContainerEntity(new AssetMetadataDataSource(Descriptor, m_DataSource, MetadataDataSourceSpecification.metadata));
            SystemMetadataEntity = new ReadOnlyMetadataContainerEntity(new AssetMetadataDataSource(Descriptor, m_DataSource, MetadataDataSourceSpecification.systemMetadata));
        }

        /// <inheritdoc />
        public IAsset WithProject(ProjectDescriptor projectDescriptor)
        {
            if (projectDescriptor == Descriptor.ProjectDescriptor) return this;

            if (!m_LinkedProjects.Contains(projectDescriptor))
                throw new InvalidArgumentException("The asset does not belong to the specified project.");

            return Copy(new AssetDescriptor(projectDescriptor, Descriptor.AssetId, Descriptor.AssetVersion));
        }

        /// <inheritdoc />
        public async Task<IAsset> WithProjectAsync(ProjectDescriptor projectDescriptor, CancellationToken cancellationToken)
        {
            var assetDescriptor = new AssetDescriptor(projectDescriptor, Descriptor.AssetId, Descriptor.AssetVersion);
            var data = await m_DataSource.GetAssetAsync(assetDescriptor, FieldsFilter.DefaultAssetIncludes, cancellationToken);

            return data.From(m_DataSource, assetDescriptor, FieldsFilter.DefaultAssetIncludes);
        }

        /// <inheritdoc />
        public async Task<IAsset> WithVersionAsync(AssetVersion assetVersion, CancellationToken cancellationToken)
        {
            var assetDescriptor = new AssetDescriptor(Descriptor.ProjectDescriptor, Descriptor.AssetId, assetVersion);
            var data = await m_DataSource.GetAssetAsync(assetDescriptor, FieldsFilter.DefaultAssetIncludes, cancellationToken);

            return data.From(m_DataSource, assetDescriptor, FieldsFilter.DefaultAssetIncludes);
        }

        /// <inheritdoc />
        /// <exception cref="NotFoundException">If a version with the corresponding <paramref name="label"/> is not found. </exception>
        public async Task<IAsset> WithVersionAsync(string label, CancellationToken cancellationToken)
        {
            var data = await m_DataSource.GetAssetAsync(Descriptor.ProjectDescriptor, Descriptor.AssetId, label, FieldsFilter.DefaultAssetIncludes, cancellationToken);

            if (data == null)
            {
                throw new NotFoundException($"Could not retrieve asset with label '{label}'.");
            }

            var assetDescriptor = new AssetDescriptor(Descriptor.ProjectDescriptor, Descriptor.AssetId, data.Version);
            return data.From(m_DataSource, assetDescriptor, FieldsFilter.DefaultAssetIncludes);
        }

        /// <inheritdoc />
        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            PreviewFileUrl = null;
            MetadataEntity.Refresh();
            SystemMetadataEntity.Refresh();

            return RefreshAsync(FieldsFilter.DefaultAssetIncludes, cancellationToken);
        }

        async Task RefreshAsync(FieldsFilter fieldsFilter, CancellationToken cancellationToken)
        {
            var assetData = await m_DataSource.GetAssetAsync(Descriptor, fieldsFilter, cancellationToken);
            this.MapFrom(m_DataSource, Descriptor, assetData, fieldsFilter);
        }

        /// <inheritdoc />
        public async Task UpdateAsync(IAssetUpdate assetUpdate, CancellationToken cancellationToken)
        {
            // Update properties first
            if (assetUpdate.HasValues())
            {
                await m_DataSource.UpdateAssetAsync(Descriptor, assetUpdate.From(), cancellationToken);
            }

            // Update status flow descriptor next
            if (assetUpdate.StatusFlowDescriptor.HasValue)
            {
                await m_DataSource.UpdateAssetStatusFlowAsync(Descriptor, assetUpdate.StatusFlowDescriptor.Value, cancellationToken);
            }
        }

        /// <inheritdoc />
        [Obsolete("Use UpdateAsync(IAssetUpdate, CancellationToken) instead.")]
        public Task UpdateStatusAsync(AssetStatusAction statusAction, CancellationToken cancellationToken)
        {
            var status = IsolatedSerialization.SerializeWithConverters(statusAction, IsolatedSerialization.StringEnumConverter).Replace("\"", "");
            return m_DataSource.UpdateAssetStatusAsync(Descriptor, status, cancellationToken);
        }

        /// <inheritdoc />
        public Task UpdateStatusAsync(string statusName, CancellationToken cancellationToken)
        {
            return m_DataSource.UpdateAssetStatusAsync(Descriptor, statusName, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IAsset> CreateUnfrozenVersionAsync(CancellationToken cancellationToken)
        {
            var version = await m_DataSource.CreateUnfrozenAssetVersionAsync(Descriptor, null, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            var assetDescriptor = new AssetDescriptor(Descriptor.ProjectDescriptor, Descriptor.AssetId, version);
            var assetData = await m_DataSource.GetAssetAsync(assetDescriptor, FieldsFilter.DefaultAssetIncludes, cancellationToken);
            return assetData.From(m_DataSource, assetDescriptor, FieldsFilter.DefaultAssetIncludes);
        }

        /// <inheritdoc />
        public async Task<int> FreezeAsync(string changeLog, CancellationToken cancellationToken)
        {
            return await m_DataSource.FreezeAssetVersionAsync(Descriptor, changeLog, false, cancellationToken) ?? -1;
        }

        /// <inheritdoc />
        public Task FreezeAsync(IAssetFreeze assetFreeze, CancellationToken cancellationToken)
        {
            bool? forceFreeze = assetFreeze.Operation switch
            {
                AssetFreezeOperation.CancelTransformations => true,
                AssetFreezeOperation.IgnoreIfTransformations => false,
                _ => null
            };
            return m_DataSource.FreezeAssetVersionAsync(Descriptor, assetFreeze.ChangeLog, forceFreeze, cancellationToken);
        }

        public Task CancelPendingFreezeAsync(CancellationToken cancellationToken)
        {
            return m_DataSource.CancelFreezeAssetVersionAsync(Descriptor, cancellationToken);
        }

        /// <inheritdoc />
        public VersionQueryBuilder QueryVersions()
        {
            return new VersionQueryBuilder(m_DataSource, Descriptor.ProjectDescriptor, Descriptor.AssetId);
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<IAssetProject> GetLinkedProjectsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var projectDescriptor in m_LinkedProjects)
            {
                var data = await m_DataSource.GetProjectAsync(projectDescriptor, cancellationToken);
                yield return data.From(m_DataSource, Descriptor.OrganizationId);
            }
        }

        /// <inheritdoc />
        public async Task LinkToProjectAsync(ProjectDescriptor projectDescriptor, CancellationToken cancellationToken)
        {
            await m_DataSource.LinkAssetToProjectAsync(Descriptor, projectDescriptor, cancellationToken);

            // We shouldn't be auto-refreshing

            var data = await m_DataSource.GetAssetAsync(Descriptor, new FieldsFilter(), cancellationToken);
            SourceProject = new ProjectDescriptor(Descriptor.OrganizationId, data.SourceProjectId);
            m_LinkedProjects = data.LinkedProjectIds?
                .Select(projectId => new ProjectDescriptor(Descriptor.OrganizationId, projectId))
                .ToArray() ?? Array.Empty<ProjectDescriptor>();
        }

        /// <inheritdoc />
        public async Task UnlinkFromProjectAsync(ProjectDescriptor projectDescriptor, CancellationToken cancellationToken)
        {
            var assetDescriptor = new AssetDescriptor(projectDescriptor, Descriptor.AssetId, Descriptor.AssetVersion);
            await m_DataSource.UnlinkAssetFromProjectAsync(assetDescriptor, cancellationToken);

            // We shouldn't be auto-refreshing

            // If we are not unlinking from the current descriptor, we can fetch to refresh the linked projects.
            if (Descriptor.ProjectId != projectDescriptor.ProjectId)
            {
                var data = await m_DataSource.GetAssetAsync(Descriptor, FieldsFilter.None, cancellationToken);
                SourceProject = new ProjectDescriptor(Descriptor.OrganizationId, data.SourceProjectId);
                m_LinkedProjects = data.LinkedProjectIds?
                    .Select(projectId => new ProjectDescriptor(Descriptor.OrganizationId, projectId))
                    .ToArray() ?? Array.Empty<ProjectDescriptor>();
            }
            else // Otherwise, we remove the project from the linked projects. The descriptor path to this asset is no longer valid.
            {
                m_LinkedProjects = m_LinkedProjects.Where(descriptor => descriptor.ProjectId != projectDescriptor.ProjectId).ToArray();
            }
        }

        /// <inheritdoc />
        public async Task<Uri> GetPreviewUrlAsync(CancellationToken cancellationToken)
        {
            if (PreviewFileUrl == null)
            {
                var fieldsFilter = new FieldsFilter {AssetFields = AssetFields.previewFileUrl};
                var assetData = await m_DataSource.GetAssetAsync(Descriptor, fieldsFilter, cancellationToken);
                this.MapFrom(m_DataSource, Descriptor, assetData, fieldsFilter);
            }

            return PreviewFileUrl;
        }

        /// <inheritdoc />
        public async Task<IDictionary<string, Uri>> GetAssetDownloadUrlsAsync(CancellationToken cancellationToken)
        {
            var fileUrls = await m_DataSource.GetAssetDownloadUrlsAsync(Descriptor, null, cancellationToken);

            var urls = new Dictionary<string, Uri>();
            foreach (var url in fileUrls)
            {
                urls.Add(url.FilePath, url.DownloadUrl);
            }

            return urls;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<CollectionDescriptor> ListLinkedAssetCollectionsAsync(Range range, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var enumerable = await m_DataSource.GetAssetCollectionsAsync(Descriptor, cancellationToken);

            var collectionDatas = enumerable?.ToArray() ?? Array.Empty<IAssetCollectionData>();
            if (collectionDatas.Length > 0)
            {
                var (start, length) = range.GetValidatedOffsetAndLength(collectionDatas.Length);
                for (var i = start; i < start + length; ++i)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    yield return new CollectionDescriptor(Descriptor.ProjectDescriptor, collectionDatas[i].GetFullCollectionPath());
                }
            }
        }

        /// <inheritdoc />
        public Task<IDataset> CreateDatasetAsync(DatasetCreation datasetCreation, CancellationToken cancellationToken) => CreateDatasetAsync((IDatasetCreation)datasetCreation, cancellationToken);

        /// <inheritdoc />
        public async Task<IDataset> CreateDatasetAsync(IDatasetCreation datasetCreation, CancellationToken cancellationToken)
        {
            var datasetData = await m_DataSource.CreateDatasetAsync(Descriptor, datasetCreation.From(), cancellationToken);
            var dataset = datasetData.From(m_DataSource, Descriptor, DatasetFields.all);

            return dataset;
        }

        /// <inheritdoc />
        public async Task<IDataset> GetDatasetAsync(DatasetId datasetId, CancellationToken cancellationToken)
        {
            var datasetDescriptor = new DatasetDescriptor(Descriptor, datasetId);
            var data = await m_DataSource.GetDatasetAsync(datasetDescriptor, FieldsFilter.DefaultDatasetIncludes, cancellationToken);
            return data?.From(m_DataSource, datasetDescriptor, FieldsFilter.DefaultDatasetIncludes.DatasetFields);
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<IDataset> ListDatasetsAsync(Range range, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var data = m_DataSource.ListDatasetsAsync(Descriptor, range, FieldsFilter.DefaultDatasetIncludes, cancellationToken);
            await foreach (var datasetData in data)
            {
                yield return datasetData.From(m_DataSource, Descriptor, FieldsFilter.DefaultDatasetIncludes.DatasetFields);
            }
        }

        /// <inheritdoc />
        public async Task<IFile> GetFileAsync(string filePath, CancellationToken cancellationToken)
        {
            var asset = await m_DataSource.GetAssetAsync(Descriptor, FieldsFilter.DefaultFileIncludes, cancellationToken);

            IFile file = null;

            var data = asset.Files?.FirstOrDefault(x => x.Path == filePath);
            if (data != null)
            {
                file = data.From(m_DataSource, Descriptor, FieldsFilter.DefaultFileIncludes.FileFields);
            }

            if (file == null)
            {
                throw new NotFoundException($"File {filePath} not found.");
            }

            return file;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<IFile> ListFilesAsync(Range range, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var asset = await m_DataSource.GetAssetAsync(Descriptor, FieldsFilter.DefaultFileIncludes, cancellationToken);

            var files = asset.Files?.ToArray() ?? Array.Empty<IFileData>();
            var (start, length) = range.GetValidatedOffsetAndLength(files.Length);
            for (var i = start; i < start + length; ++i)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return files[i].From(m_DataSource, Descriptor, FieldsFilter.DefaultFileIncludes.FileFields);
            }
        }

        /// <inheritdoc />
        public AssetLabelQueryBuilder QueryLabels()
        {
            return new AssetLabelQueryBuilder(m_DataSource, Descriptor.ProjectDescriptor, Descriptor.AssetId);
        }

        /// <inheritdoc />
        public Task AssignLabelsAsync(IEnumerable<string> labels, CancellationToken cancellationToken)
        {
            return m_DataSource.AssignLabelsAsync(Descriptor, labels, cancellationToken);
        }

        /// <inheritdoc />
        public Task UnassignLabelsAsync(IEnumerable<string> labels, CancellationToken cancellationToken)
        {
            return m_DataSource.UnassignLabelsAsync(Descriptor, labels, cancellationToken);
        }

        /// <inheritdoc />
        public Task<string[]> GetReachableStatusNamesAsync(CancellationToken cancellationToken)
        {
            return m_DataSource.GetReachableStatusNamesAsync(Descriptor, cancellationToken);
        }

        /// <inheritdoc />
        public IAsyncEnumerable<IAssetReference> ListReferencesAsync(Range range, CancellationToken cancellationToken)
        {
            var filter = new AssetReferenceSearchFilter();
            filter.AssetVersion.WhereEquals(Descriptor.AssetVersion);

            return new AssetReferenceQueryBuilder(m_DataSource, Descriptor.ProjectDescriptor, Descriptor.AssetId)
                .SelectWhereMatchesFilter(filter)
                .LimitTo(range)
                .ExecuteAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<IAssetReference> AddReferenceAsync(AssetId targetAssetId, AssetVersion targetAssetVersion, CancellationToken cancellationToken)
        {
            var assetIdentifier = new AssetIdentifierDto
            {
                Id = targetAssetId.ToString(),
                Version = targetAssetVersion.ToString()
            };
            return AddReferenceAsync(assetIdentifier, cancellationToken);
        }

        /// <inheritdoc />
        public Task<IAssetReference> AddReferenceAsync(AssetId targetAssetId, string targetLabel, CancellationToken cancellationToken)
        {
            var assetIdentifier = new AssetIdentifierDto
            {
                Id = targetAssetId.ToString(),
                Label = targetLabel
            };
            return AddReferenceAsync(assetIdentifier, cancellationToken);
        }

        async Task<IAssetReference> AddReferenceAsync(AssetIdentifierDto targetAssetIdentifier, CancellationToken cancellationToken)
        {
            var referenceId = await m_DataSource.CreateAssetReferenceAsync(Descriptor, targetAssetIdentifier, cancellationToken);
            // Ideally we would fetch the newly created reference data here, but the API does not have an entry for returning a single reference.
            return new AssetReference(Descriptor.ProjectDescriptor, referenceId)
            {
                IsValid = true,
                SourceAssetId = Descriptor.AssetId,
                SourceAssetVersion = Descriptor.AssetVersion,
                TargetAssetId = new AssetId(targetAssetIdentifier.Id),
                TargetAssetVersion = string.IsNullOrWhiteSpace(targetAssetIdentifier.Version) ? null : new AssetVersion(targetAssetIdentifier.Version),
                TargetLabel = string.IsNullOrWhiteSpace(targetAssetIdentifier.Label) ? null : targetAssetIdentifier.Label,
            };
        }

        /// <inheritdoc />
        public Task RemoveReferenceAsync(string referenceId, CancellationToken cancellationToken)
        {
            return m_DataSource.DeleteAssetReferenceAsync(Descriptor.ProjectDescriptor, Descriptor.AssetId, referenceId, cancellationToken);
        }

        /// <inheritdoc />
        public string SerializeIdentifiers()
        {
            return Descriptor.ToJson();
        }

        /// <inheritdoc />
        public string Serialize()
        {
            var data = new AssetDataWithIdentifiers
            {
                Descriptor = Descriptor.ToJson(),
                Data = this.From()
            };
            return IsolatedSerialization.SerializeWithDefaultConverters(data);
        }

        IAsset Copy(AssetDescriptor assetDescriptor)
        {
            return new AssetEntity(m_DataSource, assetDescriptor)
            {
                m_LinkedProjects = m_LinkedProjects.ToArray(),
                SourceProject = SourceProject,
                Name = Name,
                Description = Description,
                Tags = Tags?.ToArray(),
                SystemTags = SystemTags?.ToArray(),
                Labels = Labels?.ToArray(),
                ArchivedLabels = ArchivedLabels?.ToArray(),
                Type = Type,
                PreviewFile = PreviewFile,
                Status = Status,
                StatusName = StatusName,
                State = State,
                AuthoringInfo = AuthoringInfo,
                MetadataEntity = {Properties = MetadataEntity.Properties},
                SystemMetadataEntity = {Properties = SystemMetadataEntity.Properties}
            };
        }
    }
}
