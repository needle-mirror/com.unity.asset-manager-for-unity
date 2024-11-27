using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// This class contains all the information about a cloud project.
    /// </summary>
    interface IAssetProject
    {
        /// <summary>
        /// The descriptor of the project.
        /// </summary>
        ProjectDescriptor Descriptor { get; }

        /// <summary>
        /// The project name.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// The project metadata.
        /// </summary>
        IDeserializable Metadata { get; set; }

        /// <summary>
        /// Whether the project has any collections
        /// </summary>
        bool HasCollection => false;

        /// <summary>
        /// Retrieves an asset by its ID.
        /// </summary>
        /// <param name="assetId">The id of the asset. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is the asset with its default version. </returns>
        Task<IAsset> GetAssetAsync(AssetId assetId, CancellationToken cancellationToken) => throw new NotImplementedException();

        /// <summary>
        /// Retrieves an asset by its ID and version.
        /// </summary>
        /// <param name="assetId">The id of the asset. </param>
        /// <param name="assetVersion">The version of the asset. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is the requested asset. </returns>
        Task<IAsset> GetAssetAsync(AssetId assetId, AssetVersion assetVersion, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves an asset by its ID and label.
        /// </summary>
        /// <param name="assetId">The id of the asset. </param>
        /// <param name="label">The label associated to the asset version. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is the requested asset. </returns>
        Task<IAsset> GetAssetAsync(AssetId assetId, string label, CancellationToken cancellationToken) => throw new NotImplementedException();

        /// <summary>
        /// Returns an object that can be used to query asset references.
        /// </summary>
        /// <param name="assetId">The id of the asset to query. </param>
        /// <returns>A <see cref="AssetReferenceQueryBuilder"/>. </returns>
        AssetReferenceQueryBuilder QueryAssetReferences(AssetId assetId) => throw new NotImplementedException();

        /// <summary>
        /// Creates an asset.
        /// </summary>
        /// <param name="assetCreation">The object containing all the necessary information to create the asset. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is the new asset. </returns>
        Task<IAsset> CreateAssetAsync(IAssetCreation assetCreation, CancellationToken cancellationToken);

        /// <summary>
        /// Returns a builder to create a query to search a project's <see cref="IAsset"/>.
        /// </summary>
        /// <returns>An <see cref="AssetQueryBuilder"/>. </returns>
        AssetQueryBuilder QueryAssets();

        /// <summary>
        /// Returns a builder to create a query to count a project's <see cref="IAsset"/>.
        /// </summary>
        /// <returns>An <see cref="GroupAndCountAssetsQueryBuilder"/>. </returns>
        GroupAndCountAssetsQueryBuilder GroupAndCountAssets();

        /// <summary>
        /// Returns the number of assets in the project.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is the number of assets in the project. </returns>
        Task<int> CountAssetsAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

        /// <summary>
        /// Links the assets to the project.
        /// </summary>
        /// <param name="sourceProjectDescriptor">The id of the project the assets come from. </param>
        /// <param name="assetIds">The ids of assets to link. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        Task LinkAssetsAsync(ProjectDescriptor sourceProjectDescriptor, IEnumerable<AssetId> assetIds, CancellationToken cancellationToken) => throw new NotImplementedException();

        /// <summary>
        /// Unlinks the assets from the project.
        /// </summary>
        /// <param name="assetIds">The ids of assets to unlink from the project. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        Task UnlinkAssetsAsync(IEnumerable<AssetId> assetIds, CancellationToken cancellationToken) => throw new NotImplementedException();

        /// <summary>
        /// Returns a builder to create a query to search a project's <see cref="IAssetCollection"/>.
        /// </summary>
        /// <returns>A <see cref="CollectionQueryBuilder"/>. </returns>
        CollectionQueryBuilder QueryCollections();

        /// <summary>
        /// Returns the number of collections in the project.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is the number of collections in the project. </returns>
        Task<int> CountCollectionsAsync(CancellationToken cancellationToken) => throw new NotImplementedException();

        /// <summary>
        /// Returns the collection at the specified path.
        /// </summary>
        /// <param name="collectionPath">The path to the collection. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is the requested collection. </returns>
        Task<IAssetCollection> GetCollectionAsync(CollectionPath collectionPath, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a collection.
        /// </summary>
        /// <param name="assetCollectionCreation">The object containing the necessary information to create a collection. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is the newly created collection. </returns>
        Task<IAssetCollection> CreateCollectionAsync(IAssetCollectionCreation assetCollectionCreation, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes a collection.
        /// </summary>
        /// <param name="collectionPath"></param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        Task DeleteCollectionAsync(CollectionPath collectionPath, CancellationToken cancellationToken);

        /// <summary>
        /// Returns an object that can be used to query transformations.
        /// </summary>
        /// <returns>A <see cref="TransformationQueryBuilder"/>. </returns>
        TransformationQueryBuilder QueryTransformations();
    }
}
