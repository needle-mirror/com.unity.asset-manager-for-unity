using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    partial class AssetDataSource
    {
        /// <inheritdoc />
        public async Task<IPendingFileData> CreateFileAsync(DatasetDescriptor datasetDescriptor, IFileCreateData fileCreation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = new CreateFileRequest(datasetDescriptor.ProjectId, datasetDescriptor.AssetId, datasetDescriptor.AssetVersion, datasetDescriptor.DatasetId, fileCreation);
            var response = await RateLimitedServiceClient(request, HttpMethod.Post).PostAsync(GetPublicRequestUri(request), request.ConstructBody(),
                ServiceHttpClientOptions.Default(), cancellationToken);

            var jsonContent = await response.GetContentAsString();
            cancellationToken.ThrowIfCancellationRequested();

            var dto = JsonSerialization.Deserialize<UploadUrlDto>(jsonContent);

            return new PendingFileData
            {
                Path = fileCreation.Path,
                Description = fileCreation.Description,
                Metadata = fileCreation.Metadata,
                Tags = fileCreation.Tags != null ? new List<string>(fileCreation.Tags) : new List<string>(),
                UserChecksum = fileCreation.UserChecksum,
                UploadUrl = GetEscapedUri(dto.UploadUrl)
            };
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<IFileData> ListFilesAsync(DatasetDescriptor datasetDescriptor, Range range, FieldsFilter includedFieldsFilter, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var countRequest = new FileRequest(datasetDescriptor.ProjectId, datasetDescriptor.AssetId, datasetDescriptor.AssetVersion, datasetDescriptor.DatasetId, FileFields.none, limit: 1);

            var fileFields = includedFieldsFilter?.FileFields ?? FileFields.none;

            Func<string, int, ApiRequest> getListRequest = (next, pageSize) => new FileRequest(datasetDescriptor.ProjectId, datasetDescriptor.AssetId, datasetDescriptor.AssetVersion, datasetDescriptor.DatasetId, fileFields, next, pageSize);

            await foreach (var data in ListEntitiesAsync<FileData>(countRequest, getListRequest, range, cancellationToken))
            {
                yield return data;
            }
        }

        /// <inheritdoc />
        public async Task<IFileData> GetFileAsync(FileDescriptor fileDescriptor, FieldsFilter includedFieldsFilter, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = new FileRequest(fileDescriptor.ProjectId, fileDescriptor.AssetId, fileDescriptor.AssetVersion, fileDescriptor.DatasetId, fileDescriptor.Path,
                includedFieldsFilter?.FileFields ?? FileFields.none);
            var response = await RateLimitedServiceClient(request, HttpMethod.Get).GetAsync(GetPublicRequestUri(request), ServiceHttpClientOptions.Default(),
                cancellationToken);

            var jsonContent = await response.GetContentAsString();
            cancellationToken.ThrowIfCancellationRequested();

            return IsolatedSerialization.DeserializeWithDefaultConverters<FileData>(jsonContent);
        }

        /// <inheritdoc />
        public Task UpdateFileAsync(FileDescriptor fileDescriptor, IFileBaseData fileUpdate, CancellationToken cancellationToken)
        {
            var request = new FileRequest(fileDescriptor.ProjectId,
                fileDescriptor.AssetId,
                fileDescriptor.AssetVersion,
                fileDescriptor.Path,
                fileUpdate);

            return RateLimitedServiceClient(request, HttpClientExtensions.HttpMethodPatch).PatchAsync(GetPublicRequestUri(request), request.ConstructBody(),
                ServiceHttpClientOptions.Default(), cancellationToken);
        }

        /// <inheritdoc />
        public Task<Uri> GetFileDownloadUrlAsync(FileDescriptor fileDescriptor, int? maxDimension, CancellationToken cancellationToken)
        {
            const int minDimension = 50;

            if (maxDimension is < minDimension)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDimension), maxDimension, $"The minimum dimension for resize requests is {minDimension}.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var request = new GetFileDownloadUrlRequest(fileDescriptor.ProjectId,
                fileDescriptor.AssetId,
                fileDescriptor.AssetVersion,
                fileDescriptor.DatasetId,
                fileDescriptor.Path,
                maxDimension);

            return GetFileUrlAsync(request, cancellationToken);
        }

        /// <inheritdoc />
        public Task<Uri> GetFileUploadUrlAsync(FileDescriptor fileDescriptor, IFileData fileData, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = new GetFileUploadUrlRequest(fileDescriptor.ProjectId,
                fileDescriptor.AssetId,
                fileDescriptor.AssetVersion,
                fileDescriptor.DatasetId,
                fileDescriptor.Path,
                fileData);

            return GetFileUrlAsync(request, cancellationToken);
        }

        async Task<Uri> GetFileUrlAsync(ApiRequest request, CancellationToken cancellationToken)
        {
            var response = await RateLimitedServiceClient(request, HttpMethod.Get).GetAsync(GetPublicRequestUri(request), ServiceHttpClientOptions.Default(), cancellationToken);

            var jsonContent = await response.GetContentAsString();
            cancellationToken.ThrowIfCancellationRequested();

            var dto = JsonSerialization.Deserialize<FileUrl>(jsonContent);

            return GetEscapedUri(dto.Url);
        }

        /// <inheritdoc />
        public Task FinalizeFileUploadAsync(FileDescriptor fileDescriptor, bool disableAutomaticTransformations, CancellationToken cancellationToken)
        {
#if INTERNAL_ASSETS_V2_LABEL
            var request = new FinalizeFileUploadRequestV2(fileDescriptor.ProjectId,
                fileDescriptor.AssetId,
                fileDescriptor.AssetVersion,
                fileDescriptor.DatasetId,
                fileDescriptor.Path,
                disableAutomaticTransformations);

            var url = m_PublicServiceHostResolver.GetResolvedRequestUri(request.ConstructUrl("/assets/v2"));

            return RateLimitedServiceClient(request, HttpMethod.Post).PostAsync(url, request.ConstructBody(),
                ServiceHttpClientOptions.Default(), cancellationToken);

#elif INTERNAL_ASSETS_V2_URL
            var request = new FinalizeFileUploadRequestV2(fileDescriptor.ProjectId,
                fileDescriptor.AssetId,
                fileDescriptor.AssetVersion,
                fileDescriptor.DatasetId,
                fileDescriptor.Path,
                disableAutomaticTransformations);

            var url = m_PublicServiceHostResolver.GetResolvedRequestUri(request.ConstructUrl("/assets/v2alpha1"));

            return RateLimitedServiceClient(request, HttpMethod.Post).PostAsync(url, request.ConstructBody(),
                ServiceHttpClientOptions.Default(), cancellationToken);
#else
            var request = new FinalizeFileUploadRequestV1(fileDescriptor.ProjectId,
                fileDescriptor.AssetId,
                fileDescriptor.AssetVersion,
                fileDescriptor.Path,
                disableAutomaticTransformations);

            return RateLimitedServiceClient(request, HttpMethod.Post).PostAsync(GetPublicRequestUri(request), request.ConstructBody(),
                ServiceHttpClientOptions.Default(), cancellationToken);
#endif
        }

        /// <inheritdoc />
        public async Task<FileTag[]> GenerateFileTagsAsync(FileDescriptor fileDescriptor, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = new GenerateFileTagsRequest(fileDescriptor.ProjectId,
                fileDescriptor.AssetId,
                fileDescriptor.AssetVersion,
                fileDescriptor.DatasetId,
                fileDescriptor.Path);
            var response = await RateLimitedServiceClient(request, HttpMethod.Get).GetAsync(GetPublicRequestUri(request),
                ServiceHttpClientOptions.Default(), cancellationToken);

            var jsonContent = await response.GetContentAsString();
            cancellationToken.ThrowIfCancellationRequested();

            var dto = JsonSerialization.Deserialize<FileTags>(jsonContent);

            return dto.Tags;
        }

        /// <inheritdoc />
        public Task RemoveFileMetadataAsync(FileDescriptor fileDescriptor, string metadataType, IEnumerable<string> keys, CancellationToken cancellationToken)
        {
            var request = new RemoveMetadataRequest(fileDescriptor.ProjectId,
                fileDescriptor.AssetId,
                fileDescriptor.AssetVersion,
                fileDescriptor.DatasetId,
                fileDescriptor.Path,
                metadataType,
                keys);
            return RateLimitedServiceClient(request, HttpMethod.Delete).DeleteAsync(GetPublicRequestUri(request), ServiceHttpClientOptions.Default(), cancellationToken);
        }
    }
}
