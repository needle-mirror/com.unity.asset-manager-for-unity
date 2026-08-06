using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    partial class AssetDataSource
    {
        /// <inheritdoc />
        public async IAsyncEnumerable<IAssetData> ListAssetsInTrashAsync(ProjectDescriptor projectDescriptor, SearchRequestParameters parameters, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (offset, length) = await parameters.PaginationRange.GetOffsetAndLengthAsync(token => GetAssetCountAsync(projectDescriptor, token), cancellationToken);
            if (length == 0) yield break;

            var request = new SearchAssetsInTrashRequest(projectDescriptor.ProjectId, parameters);

            var results = ListAssetsAsync(request, parameters, offset, length, cancellationToken);
            await foreach (var asset in results)
            {
                yield return asset;
            }
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<IAssetData> ListAssetsInTrashAcrossProjectsAsync(OrganizationId organizationId, IEnumerable<ProjectId> projectIds, SearchRequestParameters parameters, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (offset, length) = await parameters.PaginationRange.GetOffsetAndLengthAsync(token => GetAcrossProjectsTotalCount(organizationId, projectIds, token), cancellationToken);
            if (length == 0) yield break;

            var request = new SearchAssetsInTrashRequest(organizationId, parameters);
            var results = ListAssetsAsync(request, parameters, offset, length, cancellationToken);
            await foreach (var asset in results.WithCancellation(cancellationToken))
            {
                yield return asset;
            }
        }

        /// <inheritdoc />
        public async Task<IAssetData> GetAssetFromTrashAsync(AssetDescriptor assetDescriptor, FieldsFilter fieldsFilter, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = new GetAssetInTrashRequest(assetDescriptor.ProjectId, assetDescriptor.AssetId, assetDescriptor.AssetVersion, fieldsFilter);
            using var response = await m_ServiceHttpClient.GetAsync(GetPublicRequestUri(request), ServiceHttpClientOptions.Default(), cancellationToken);

            var jsonContent = await response.GetContentAsStringAsync();
            cancellationToken.ThrowIfCancellationRequested();

            var assetData = IsolatedSerialization.DeserializeWithDefaultConverters<AssetData>(jsonContent);
            return assetData;
        }

        /// <inheritdoc />
        public async Task RestoreAssetsFromTrashAsync(ProjectDescriptor projectDescriptor, IEnumerable<AssetId> assetIds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = new RestoreAssetsFromTrashRequest(projectDescriptor.ProjectId, assetIds);
            using var _ = await m_ServiceHttpClient.PostAsync(GetPublicRequestUri(request), request.ConstructBody(),
                ServiceHttpClientOptions.Default(), cancellationToken);
        }

        /// <inheritdoc />
        public async Task TrashAssetsAsync(ProjectDescriptor projectDescriptor, IEnumerable<AssetId> assetIds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = new UnlinkAssetFromProjectRequest(projectDescriptor.ProjectId, assetIds, true);
            using var _ = await m_ServiceHttpClient.PostAsync(GetPublicRequestUri(request), request.ConstructBody(),
                ServiceHttpClientOptions.Default(), cancellationToken);
        }

        /// <inheritdoc />
        public async Task DeleteAssetsFromTrashAsync(ProjectDescriptor projectDescriptor, IEnumerable<AssetId> assetIds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = TrashRequest.DeleteAssets(projectDescriptor.ProjectId, assetIds);
            using var _ = await m_ServiceHttpClient.DeleteAsync(GetPublicRequestUri(request), request.ConstructBody(),
                ServiceHttpClientOptions.Default(), cancellationToken);
        }

        /// <inheritdoc />
        public async Task EmptyTrashAsync(ProjectDescriptor projectDescriptor, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var request = TrashRequest.Empty(projectDescriptor.ProjectId);
            using var _ = await m_ServiceHttpClient.DeleteAsync(GetPublicRequestUri(request), request.ConstructBody(),
                ServiceHttpClientOptions.Default(), cancellationToken);
        }
    }
}
