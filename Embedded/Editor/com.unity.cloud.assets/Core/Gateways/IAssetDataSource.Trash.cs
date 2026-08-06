using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    partial interface IAssetDataSource
    {
        /// <summary>
        /// Retrieves the assets from a project's trash given the criteria.
        /// </summary>
        /// <param name="projectDescriptor">The object containing the necessary information to identify the project the asset belongs to.</param>
        /// <param name="parameters">The search parameters. </param>
        /// <param name="cancellationToken"></param>
        /// <returns>A task whose result is an async enumeration of <see cref="IAssetData"/>. </returns>
        IAsyncEnumerable<IAssetData> ListAssetsInTrashAsync(ProjectDescriptor projectDescriptor, SearchRequestParameters parameters, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves the assets from the trash across multiple projects given the criteria.
        /// </summary>
        /// <param name="organizationId">The organization id. </param>
        /// <param name="projectIds">The ids of the projects in which to search. </param>
        /// <param name="parameters">An object containing the parameters of a search. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request.</param>
        /// <returns>A task whose result is an async enumeration of <see cref="IAssetData"/>. </returns>
        IAsyncEnumerable<IAssetData> ListAssetsInTrashAcrossProjectsAsync(OrganizationId organizationId, IEnumerable<ProjectId> projectIds, SearchRequestParameters parameters, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves an <see cref="IAssetData"/>.
        /// </summary>
        /// <param name="assetDescriptor">The object containing the necessary information to identify the asset.</param>
        /// <param name="fieldsFilter">The fields filter defining which fields to include in the response.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the request.</param>
        /// <returns>A task whose result is a <see cref="IAssetData"/>. </returns>
        Task<IAssetData> GetAssetFromTrashAsync(AssetDescriptor assetDescriptor, FieldsFilter fieldsFilter, CancellationToken cancellationToken);

        /// <summary>
        /// Restores the assets from the trash back to the project.
        /// </summary>
        /// <param name="projectDescriptor">The object containing the necessary information to identify the project.</param>
        /// <param name="assetIds">The ids of the assets to restore.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the request.</param>
        /// <returns>A task with no result. </returns>
        Task RestoreAssetsFromTrashAsync(ProjectDescriptor projectDescriptor, IEnumerable<AssetId> assetIds, CancellationToken cancellationToken);

        /// <summary>
        /// Trashes the assets from the project.
        /// </summary>
        /// <param name="projectDescriptor">The object containing the necessary information to identify the project.</param>
        /// <param name="assetIds">The ids of the assets to trash.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the request.</param>
        /// <returns>A task with no result. </returns>
        Task TrashAssetsAsync(ProjectDescriptor projectDescriptor, IEnumerable<AssetId> assetIds, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes permanently the assets from the trash.
        /// </summary>
        /// <param name="projectDescriptor">The object containing the necessary information to identify the project.</param>
        /// <param name="assetIds">The ids of the assets to trash.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the request.</param>
        /// <returns>A task with no result. </returns>
        Task DeleteAssetsFromTrashAsync(ProjectDescriptor projectDescriptor, IEnumerable<AssetId> assetIds, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes permanently all assets from the trash.
        /// </summary>
        /// <param name="projectDescriptor">The object containing the necessary information to identify the project.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the request.</param>
        /// <returns>A task with no result. </returns>
        Task EmptyTrashAsync(ProjectDescriptor projectDescriptor, CancellationToken cancellationToken);
    }
}
