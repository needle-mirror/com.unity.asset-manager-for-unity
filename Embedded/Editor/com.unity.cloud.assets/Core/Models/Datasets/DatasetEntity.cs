using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    class DatasetEntity : IDataset
    {
        const int k_MD5_bufferSize = 4096;

        readonly IAssetDataSource m_DataSource;

        List<string> m_FileOrder = new();

        /// <inheritdoc />
        public DatasetDescriptor Descriptor { get; }

        /// <inheritdoc />
        public string Name { get; set; }

        /// <inheritdoc />
        public string Description { get; set; }

        /// <inheritdoc />
        public IEnumerable<string> Tags { get; set; }

        /// <inheritdoc />
        public IEnumerable<string> SystemTags { get; set; }

        /// <inheritdoc />
        public string Status { get; set; }

        /// <inheritdoc />
        public AuthoringInfo AuthoringInfo { get; set; }

        /// <inheritdoc />
        public IMetadataContainer Metadata => MetadataEntity;

        /// <inheritdoc />
        public IReadOnlyMetadataContainer SystemMetadata => SystemMetadataEntity;

        /// <inheritdoc />
        public IEnumerable<string> FileOrder
        {
            get => m_FileOrder;
            set => m_FileOrder = value?.ToList() ?? new List<string>();
        }

        /// <inheritdoc />
        public bool IsVisible { get; set; }

        internal MetadataContainerEntity MetadataEntity { get; }
        internal ReadOnlyMetadataContainerEntity SystemMetadataEntity { get; }

        /// <summary>
        /// The name of the workflow.
        /// </summary>
        internal string WorkflowName { get; set; }

        internal DatasetEntity(IAssetDataSource assetDataSource, DatasetDescriptor datasetDescriptor)
        {
            m_DataSource = assetDataSource;
            Descriptor = datasetDescriptor;

            MetadataEntity = new MetadataContainerEntity(new DatasetMetadataDataSource(Descriptor, m_DataSource, MetadataDataSourceSpecification.metadata));
            SystemMetadataEntity = new ReadOnlyMetadataContainerEntity(new DatasetMetadataDataSource(Descriptor, m_DataSource, MetadataDataSourceSpecification.systemMetadata));
        }

        /// <inheritdoc />
        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            MetadataEntity.Refresh();
            SystemMetadataEntity.Refresh();

            return RefreshAsync(FieldsFilter.DefaultDatasetIncludes, cancellationToken);
        }

        async Task RefreshAsync(FieldsFilter fieldsFilter, CancellationToken cancellationToken)
        {
            var data = await m_DataSource.GetDatasetAsync(Descriptor, fieldsFilter, cancellationToken);
            this.MapFrom(m_DataSource, data, fieldsFilter.DatasetFields);
        }

        /// <inheritdoc />
        public async Task UpdateAsync(IDatasetUpdate datasetUpdate, CancellationToken cancellationToken)
        {
            await m_DataSource.UpdateDatasetAsync(Descriptor, datasetUpdate.From(), cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IFile> AddExistingFileAsync(string filePath, DatasetId sourceDatasetId, CancellationToken cancellationToken)
        {
            await m_DataSource.ReferenceFileFromDatasetAsync(Descriptor, sourceDatasetId, filePath, cancellationToken);

            return await GetFileAsync(filePath, cancellationToken);
        }

        /// <inheritdoc />
        public async Task RemoveFileAsync(string filePath, CancellationToken cancellationToken)
        {
            await m_DataSource.RemoveFileFromDatasetAsync(Descriptor, filePath, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IFile> GetFileAsync(string filePath, CancellationToken cancellationToken)
        {
            var fileDescriptor = new FileDescriptor(Descriptor, filePath);
            var data = await m_DataSource.GetFileAsync(fileDescriptor, FieldsFilter.DefaultFileIncludes, cancellationToken);
            return data?.From(m_DataSource, fileDescriptor, FieldsFilter.DefaultFileIncludes.FileFields);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyDictionary<string, Uri>> GetDownloadUrlsAsync(CancellationToken cancellationToken)
        {
            var fileUrls = await m_DataSource.GetAssetDownloadUrlsAsync(Descriptor.AssetDescriptor, new[] {Descriptor.DatasetId}, cancellationToken);

            var urls = new Dictionary<string, Uri>();
            foreach (var url in fileUrls)
            {
                urls.Add(url.FilePath, url.DownloadUrl);
            }

            return urls;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<IFile> ListFilesAsync(Range range, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var data = m_DataSource.ListFilesAsync(Descriptor, range, FieldsFilter.DefaultFileIncludes, cancellationToken);
            await foreach (var fileData in data)
            {
                yield return fileData.From(m_DataSource, new FileDescriptor(Descriptor, fileData.Path), FieldsFilter.DefaultFileIncludes.FileFields);
            }
        }

        /// <inheritdoc />
        public Uri GetFileUrl(string filePath)
        {
            filePath = Uri.EscapeDataString(filePath);
            var fileUriBuilder = new UriBuilder(m_DataSource.GetServiceUrl())
            {
                Path = $"assets/storage/v1/projects/{Descriptor.ProjectId}/assets/{Descriptor.AssetId}/versions/{Descriptor.AssetVersion}/datasets/{Descriptor.DatasetId}/files/{filePath}"
            };

            return fileUriBuilder.Uri;
        }

        /// <inheritdoc />
        public async Task<IFile> UploadFileAsync(IFileCreation fileCreation, Stream sourceStream, IProgress<HttpProgress> progress, CancellationToken cancellationToken)
        {
            var filePath = fileCreation.Path.Replace('\\', '/');

            var checksum = await CalculateMD5ChecksumAsync(sourceStream, cancellationToken);

            var createInternal = new FileCreateData
            {
                Path = filePath,
                Description = fileCreation.Description,
                Metadata = fileCreation.Metadata?.ToObjectDictionary() ?? new Dictionary<string, object>(),
                UserChecksum = checksum,
                SizeBytes = sourceStream.Length,
                Tags = fileCreation.Tags?.ToList() ?? new List<string>(), // WORKAROUND until backend supports null tags
            };

            var pendingfile = await m_DataSource.CreateFileAsync(Descriptor, createInternal, cancellationToken);
            if (cancellationToken.IsCancellationRequested) // if file was created but external code requested cancellation
            {
                await m_DataSource.RemoveFileFromDatasetAsync(Descriptor, filePath, default);
            }

            if (pendingfile.UploadUrl != null) //file is new for this dataset, needs to be uploaded
            {
                try
                {
                    await m_DataSource.UploadContentAsync(pendingfile.UploadUrl, sourceStream, progress, cancellationToken);
                    await m_DataSource.FinalizeFileUploadAsync(new FileDescriptor(Descriptor, filePath), fileCreation.DisableAutomaticTransformations, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    await m_DataSource.RemoveFileFromDatasetAsync(Descriptor, filePath, default);
                    throw;
                }
            }

            return await GetFileAsync(filePath, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<ITransformation> StartTransformationAsync(ITransformationCreation transformationCreation, CancellationToken cancellationToken)
        {
            string workflowName;
            switch (transformationCreation.WorkflowType)
            {
                case WorkflowType.Custom:
                    if (string.IsNullOrEmpty(transformationCreation.CustomWorkflowName))
                    {
                        throw new InvalidArgumentException($"A workflow name must be provided when {nameof(WorkflowType.Custom)} is selected.");
                    }
                    workflowName = transformationCreation.CustomWorkflowName;
                    break;
                default:
                    workflowName = transformationCreation.WorkflowType.ToJsonValue();
                    break;
            }

            var transformationId = await m_DataSource.StartTransformationAsync(Descriptor, workflowName, transformationCreation.InputFilePaths, transformationCreation.GetParameters(), cancellationToken);
            var transformation = await GetTransformationAsync(transformationId, cancellationToken);

            return transformation;
        }

        /// <inheritdoc />
        public IAsyncEnumerable<ITransformation> ListTransformationsAsync(Range range, CancellationToken cancellationToken)
        {
            var searchFilter = new TransformationSearchFilter();
            searchFilter.AssetId.WhereEquals(Descriptor.AssetId);
            searchFilter.AssetVersion.WhereEquals(Descriptor.AssetVersion);
            searchFilter.DatasetId.WhereEquals(Descriptor.DatasetId);

            return new TransformationQueryBuilder(m_DataSource, Descriptor.AssetDescriptor.ProjectDescriptor)
                .SelectWhereMatchesFilter(searchFilter)
                .LimitTo(range)
                .ExecuteAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<ITransformation> GetTransformationAsync(TransformationId transformationId, CancellationToken cancellationToken)
        {
            var descriptor = new TransformationDescriptor(Descriptor, transformationId);
            var transformation = new TransformationEntity(m_DataSource, descriptor);

            var data = await m_DataSource.GetTransformationAsync(descriptor, cancellationToken);

            transformation.MapFrom(data);
            return transformation;
        }

        static async Task<string> CalculateMD5ChecksumAsync(Stream stream, CancellationToken cancellationToken)
        {
            var position = stream.Position;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                //In this method, MD5 algorythm is used for calculating checksum of a stream or a file before uploading it.
                //It is not used in a sensitive context.
#pragma warning disable S4790 //Using weak hashing algorithms is security-sensitive
                using (var md5 = MD5.Create())
#pragma warning restore S4790
                {
                    var result = new TaskCompletionSource<bool>();
                    await TaskUtils.Run(async () =>
                    {
                        try
                        {
                            await CalculateMD5ChecksumInternalAsync(md5, stream, cancellationToken);
                        }
                        finally
                        {
                            result.SetResult(true);
                        }
                    }, cancellationToken);
                    await result.Task;
                    return BitConverter.ToString(md5.Hash).Replace("-", "").ToLowerInvariant();
                }
            }
            finally
            {
                stream.Position = position;
            }
        }

        static async Task CalculateMD5ChecksumInternalAsync(MD5 md5, Stream stream, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[k_MD5_bufferSize];
            int bytesRead;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();
#if UNITY_WEBGL && !UNITY_EDITOR
                bytesRead = await Task.FromResult(stream.Read(buffer, 0, k_MD5_bufferSize));
#else
                bytesRead = await stream.ReadAsync(buffer, 0, k_MD5_bufferSize, cancellationToken);
#endif
                if (bytesRead > 0)
                {
                    md5.TransformBlock(buffer, 0, bytesRead, null, 0);
                }
            } while (bytesRead > 0);

            md5.TransformFinalBlock(buffer, 0, 0);
            await Task.CompletedTask;
        }
    }
}
