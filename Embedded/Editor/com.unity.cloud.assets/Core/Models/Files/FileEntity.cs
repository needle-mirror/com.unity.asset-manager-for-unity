using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    class FileEntity : IFile
    {
        readonly IAssetDataSource m_DataSource;
        internal DatasetDescriptor[] m_LinkedDatasets = Array.Empty<DatasetDescriptor>();

        internal FileEntity(IAssetDataSource dataSource, FileDescriptor descriptor)
        {
            m_DataSource = dataSource;
            Descriptor = descriptor;

            MetadataEntity = new MetadataContainerEntity(new FileMetadataDataSource(Descriptor, m_DataSource, MetadataDataSourceSpecification.metadata));
            SystemMetadataEntity = new ReadOnlyMetadataContainerEntity(new FileMetadataDataSource(Descriptor, m_DataSource, MetadataDataSourceSpecification.systemMetadata));
        }

        /// <inheritdoc />
        public FileDescriptor Descriptor { get; }

        /// <inheritdoc />
        public string Description { get; set; }

        /// <inheritdoc />
        public string Status { get; set; }

        /// <inheritdoc />
        public AuthoringInfo AuthoringInfo { get; set; }

        /// <inheritdoc />
        public IEnumerable<string> Tags { get; set; }

        /// <inheritdoc />
        public IEnumerable<string> SystemTags { get; set; }

        /// <inheritdoc />
        public IMetadataContainer Metadata => MetadataEntity;

        /// <inheritdoc />
        public IReadOnlyMetadataContainer SystemMetadata => SystemMetadataEntity;

        /// <inheritdoc />
        public IEnumerable<DatasetDescriptor> LinkedDatasets => m_LinkedDatasets;

        /// <inheritdoc />
        public long SizeBytes { get; set; }

        public string UserChecksum { get; set; }

        internal Uri PreviewUrl { get; set; }

        internal Uri UploadUrl { get; set; }

        internal Uri DownloadUrl { get; set; }

        internal bool IsDownloadable { get; set; } = true;

        internal MetadataContainerEntity MetadataEntity { get; }
        internal ReadOnlyMetadataContainerEntity SystemMetadataEntity { get; }

        AssetDescriptor AssetDescriptor => Descriptor.DatasetDescriptor.AssetDescriptor;

        /// <inheritdoc />
        public IFile WithDataset(DatasetDescriptor datasetDescriptor)
        {
            if (datasetDescriptor == Descriptor.DatasetDescriptor) return this;

            if (!m_LinkedDatasets.Contains(datasetDescriptor))
                throw new InvalidArgumentException("The file does not belong to the specified dataset.");

            var descriptor = new FileDescriptor(datasetDescriptor, Descriptor.Path);
            return new FileEntity(m_DataSource, descriptor)
            {
                m_LinkedDatasets = m_LinkedDatasets,
                Description = Description,
                Status = Status,
                AuthoringInfo = AuthoringInfo,
                Tags = Tags?.ToArray(),
                SystemTags = SystemTags?.ToArray(),
                MetadataEntity = {Properties = MetadataEntity.Properties},
                SystemMetadataEntity = {Properties = SystemMetadataEntity.Properties},
                SizeBytes = SizeBytes,
                UserChecksum = UserChecksum,
                PreviewUrl = PreviewUrl,
                UploadUrl = UploadUrl,
                DownloadUrl = DownloadUrl,
                IsDownloadable = IsDownloadable
            };
        }

        /// <inheritdoc />
        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            PreviewUrl = null;
            DownloadUrl = null;
            UploadUrl = null;
            MetadataEntity.Refresh();
            SystemMetadataEntity.Refresh();

            return RefreshAsync(FieldsFilter.DefaultFileIncludes, cancellationToken);
        }

        async Task RefreshAsync(FieldsFilter fieldsFilter, CancellationToken cancellationToken)
        {
            var fileData = await m_DataSource.GetFileAsync(Descriptor, fieldsFilter, cancellationToken);
            this.MapFrom(m_DataSource, Descriptor.DatasetDescriptor.AssetDescriptor, fileData, fieldsFilter.FileFields);
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<IDataset> GetLinkedDatasetsAsync(Range range, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var (start, length) = range.GetValidatedOffsetAndLength(m_LinkedDatasets.Length);
            for (var i = start; i < start + length; i++)
            {
                var dataset = await m_DataSource.GetDatasetAsync(m_LinkedDatasets[i], FieldsFilter.DefaultDatasetIncludes, cancellationToken);
                yield return dataset.From(m_DataSource, AssetDescriptor, FieldsFilter.DefaultDatasetIncludes.DatasetFields);
            }
        }

        /// <inherticdoc />
        public async Task<Uri> GetPreviewUrlAsync(CancellationToken cancellationToken)
        {
            if (PreviewUrl == null)
            {
                var filter = new FieldsFilter {FileFields = FileFields.previewURL};
                var fileData = await m_DataSource.GetFileAsync(Descriptor, filter, cancellationToken);
                this.MapFrom(m_DataSource, Descriptor.DatasetDescriptor.AssetDescriptor, fileData, filter.FileFields);
            }

            return PreviewUrl;
        }

        /// <inheritdoc />
        public async Task<Uri> GetDownloadUrlAsync(CancellationToken cancellationToken)
        {
            if (!IsDownloadable) return null;

            if (DownloadUrl == null)
            {
                try
                {
                    DownloadUrl = await m_DataSource.GetFileDownloadUrlAsync(Descriptor, null, cancellationToken);
                }
                catch (NotFoundException)
                {
                    IsDownloadable = false;
                    return null;
                }
            }

            return DownloadUrl;
        }

        /// <inheritdoc />
        public async Task DownloadAsync(Stream targetStream, IProgress<HttpProgress> progress, CancellationToken cancellationToken)
        {
            if (!IsDownloadable) return;

            await GetDownloadUrlAsync(cancellationToken);

            try
            {
                await m_DataSource.DownloadContentAsync(DownloadUrl, targetStream, progress, cancellationToken);
            }
            catch (NotFoundException)
            {
                // If the download fails, try to get a new download url and try again.
                DownloadUrl = null;
                await GetDownloadUrlAsync(cancellationToken);
                await m_DataSource.DownloadContentAsync(DownloadUrl, targetStream, progress, cancellationToken);
            }
            finally
            {
                DownloadUrl = null; // Discard the url as it can only be used once.
            }
        }

        /// <inheritdoc />
        public Task<Uri> GetResizedImageDownloadUrlAsync(int maxDimension, CancellationToken cancellationToken)
        {
            return m_DataSource.GetFileDownloadUrlAsync(Descriptor, maxDimension, cancellationToken);
        }

        /// Not exposed in the interface
        public async Task<Uri> GetUploadUrlAsync(CancellationToken cancellationToken)
        {
            if (UploadUrl == null)
            {
                var data = new FileData
                {
                    Path = Descriptor.Path,
                    UserChecksum = UserChecksum,
                    SizeBytes = SizeBytes
                };
                UploadUrl = await m_DataSource.GetFileUploadUrlAsync(Descriptor, data, cancellationToken);
            }

            return UploadUrl;
        }

        /// <inheritdoc />
        public async Task UpdateAsync(IFileUpdate fileUpdate, CancellationToken cancellationToken)
        {
            await m_DataSource.UpdateFileAsync(Descriptor, fileUpdate.From(), cancellationToken);
        }

        /// <inheritdoc />
        public async Task UploadAsync(Stream sourceStream, IProgress<HttpProgress> progress, CancellationToken cancellationToken)
        {
            var result = Metadata.Query().ExecuteAsync(cancellationToken);
            var metadata = new Dictionary<string, MetadataValue>();
            await foreach (var item in result)
            {
                metadata.Add(item.Key, item.Value);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var datasets = new List<IDataset>();
            var datasetList = GetLinkedDatasetsAsync(Range.All, cancellationToken);

            // Remove file from all datasets
            await foreach (var dataset in datasetList)
            {
                datasets.Add(dataset);
                await dataset.RemoveFileAsync(Descriptor.Path, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
            }

            // Reupload to dataset[0]
            var fileCreation = new FileCreation(Descriptor.Path)
            {
                Description = Description,
                Tags = Tags,
                Metadata = metadata
            };

            var newFile = await datasets[0].UploadFileAsync(fileCreation, sourceStream, progress, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Link to remaining datasets
            var tasks = new List<Task>();
            for (var i = 1; i < datasets.Count; ++i)
            {
                var task = datasets[i].AddExistingFileAsync(newFile.Descriptor.Path, newFile.Descriptor.DatasetId, cancellationToken);
                tasks.Add(task);
            }

            await Task.WhenAll(tasks);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<GeneratedTag>> GenerateSuggestedTagsAsync(CancellationToken cancellationToken)
        {
            var tags = await m_DataSource.GenerateFileTagsAsync(Descriptor, cancellationToken);
            return tags.Select(x => new GeneratedTag(x.Tag, x.Confidence));
        }
    }
}
