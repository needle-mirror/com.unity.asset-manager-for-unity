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
    sealed class AssetProjectEntity : IAssetProject
    {
        readonly IAssetDataSource m_DataSource;

        /// <inheritdoc />
        public ProjectDescriptor Descriptor { get; }

        /// <inheritdoc />
        public string Name { get; set; }

        /// <inheritdoc />
        public IDeserializable Metadata { get; set; }

        /// <inheritdoc/>
        public bool HasCollection { get; set; }

        internal AssetProjectEntity(IAssetDataSource dataSource, ProjectDescriptor projectDescriptor)
        {
            m_DataSource = dataSource;
            Descriptor = projectDescriptor;
        }

        /// <inheritdoc />
        public async Task<IAsset> GetAssetAsync(AssetId assetId, CancellationToken cancellationToken)
        {
            var filter = new AssetSearchFilter();
            filter.Include().Id.WithValue(assetId.ToString());

            var query = QueryAssets()
                .SelectWhereMatchesFilter(filter)
                .LimitTo(new Range(0, 1))
                .ExecuteAsync(cancellationToken);

            IAsset asset = null;

            var enumerator = query.GetAsyncEnumerator(cancellationToken);
            if (await enumerator.MoveNextAsync())
            {
                asset = enumerator.Current;
            }

            await enumerator.DisposeAsync();

            return asset;
        }

        /// <inheritdoc />
        public async Task<IAsset> GetAssetAsync(AssetId assetId, AssetVersion assetVersion, CancellationToken cancellationToken)
        {
            var data = await m_DataSource.GetAssetAsync(new AssetDescriptor(Descriptor, assetId, assetVersion), FieldsFilter.DefaultAssetIncludes, cancellationToken);
            return data.From(m_DataSource, Descriptor, FieldsFilter.DefaultAssetIncludes);
        }

        /// <inheritdoc />
        public async Task<IAsset> GetAssetAsync(AssetId assetId, string label, CancellationToken cancellationToken)
        {
            var data = await m_DataSource.GetAssetAsync(Descriptor, assetId, label, FieldsFilter.DefaultAssetIncludes, cancellationToken);
            return data.From(m_DataSource, Descriptor, FieldsFilter.DefaultAssetIncludes);
        }

        /// <inheritdoc />
        public AssetReferenceQueryBuilder QueryAssetReferences(AssetId assetId)
        {
            return new AssetReferenceQueryBuilder(m_DataSource, Descriptor, assetId);
        }

        /// <inheritdoc />
        public async Task<IAsset> CreateAssetAsync(IAssetCreation assetCreation, CancellationToken cancellationToken)
        {
            var data = await m_DataSource.CreateAssetAsync(Descriptor, assetCreation.From(), cancellationToken);
            return data.From(m_DataSource, Descriptor, FieldsFilter.All);
        }

        /// <inheritdoc />
        public AssetQueryBuilder QueryAssets()
        {
            return new AssetQueryBuilder(m_DataSource, Descriptor);
        }

        /// <inheritdoc />
        public GroupAndCountAssetsQueryBuilder GroupAndCountAssets()
        {
            return new GroupAndCountAssetsQueryBuilder(m_DataSource, Descriptor);
        }

        /// <inheritdoc />
        public Task<int> CountAssetsAsync(CancellationToken cancellationToken)
        {
            return m_DataSource.GetAssetCountAsync(Descriptor, cancellationToken);
        }

        /// <inheritdoc />
        public Task LinkAssetsAsync(ProjectDescriptor sourceProjectDescriptor, IEnumerable<AssetId> assetIds, CancellationToken cancellationToken)
        {
            return m_DataSource.LinkAssetsToProjectAsync(sourceProjectDescriptor, Descriptor, assetIds, cancellationToken);
        }

        /// <inheritdoc />
        public Task UnlinkAssetsAsync(IEnumerable<AssetId> assetIds, CancellationToken cancellationToken)
        {
            return m_DataSource.UnlinkAssetsFromProjectAsync(Descriptor, assetIds, cancellationToken);
        }

        /// <inheritdoc />
        public CollectionQueryBuilder QueryCollections()
        {
            return new CollectionQueryBuilder(m_DataSource, Descriptor);
        }

        /// <inheritdoc />
        public Task<int> CountCollectionsAsync(CancellationToken cancellationToken)
        {
            return m_DataSource.GetCollectionCountAsync(Descriptor, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IAssetCollection> GetCollectionAsync(CollectionPath collectionPath, CancellationToken cancellationToken)
        {
            var collectionData = await m_DataSource.GetCollectionAsync(new CollectionDescriptor(Descriptor, collectionPath), cancellationToken);
            return collectionData.From(m_DataSource, Descriptor);
        }

        /// <inheritdoc />
        public async Task<IAssetCollection> CreateCollectionAsync(IAssetCollectionCreation assetCollectionCreation, CancellationToken cancellationToken)
        {
            assetCollectionCreation.Validate();

            var creationPath = CollectionPath.CombinePaths(assetCollectionCreation.ParentPath, assetCollectionCreation.Name);
            var assetCollection = new AssetCollection(m_DataSource, new CollectionDescriptor(Descriptor, creationPath))
            {
                Description = assetCollectionCreation.Description
            };

            var collectionPath = await m_DataSource.CreateCollectionAsync(Descriptor, assetCollection.From(), cancellationToken);
            if (creationPath != collectionPath)
            {
                throw new CreateCollectionFailedException($"Failed to create a collection at path {creationPath}");
            }

            return assetCollection;
        }

        /// <inheritdoc />
        public Task DeleteCollectionAsync(CollectionPath collectionPath, CancellationToken cancellationToken)
        {
            return m_DataSource.DeleteCollectionAsync(new CollectionDescriptor(Descriptor, collectionPath), cancellationToken);
        }

        /// <inheritdoc />
        public TransformationQueryBuilder QueryTransformations()
        {
            return new TransformationQueryBuilder(m_DataSource, Descriptor);
        }
    }
}
