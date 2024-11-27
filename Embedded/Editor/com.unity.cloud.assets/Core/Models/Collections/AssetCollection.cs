using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// This object contains the information about an asset collection stored on the cloud.
    /// </summary>
    sealed class AssetCollection : IAssetCollection
    {
        readonly IAssetDataSource m_DataSource;

        /// <inheritdoc />
        public CollectionDescriptor Descriptor { get; private set; }

        /// <inheritdoc />
        public string Name => Descriptor.Path.GetLastComponentOfPath();

        /// <inheritdoc />
        public CollectionPath ParentPath => Descriptor.Path.GetParentPath();

        /// <inheritdoc />
        public string Description { get; set; }

        internal AssetCollection(IAssetDataSource dataSource, CollectionDescriptor descriptor)
        {
            m_DataSource = dataSource;
            Descriptor = descriptor;
        }

        /// <inheritdoc />
        public string GetFullCollectionPath()
        {
            return Descriptor.Path;
        }

        /// <inheritdoc />
        public async Task RefreshAsync(CancellationToken cancellationToken)
        {
            var data = await m_DataSource.GetCollectionAsync(Descriptor, cancellationToken);
            Description = data.Description;
        }

        /// <inheritdoc />
        public async Task UpdateAsync(IAssetCollectionUpdate assetCollectionUpdate, CancellationToken cancellationToken)
        {
            await m_DataSource.UpdateCollectionAsync(Descriptor, assetCollectionUpdate.From(), cancellationToken);

            var newPath = CollectionPath.CombinePaths(ParentPath, assetCollectionUpdate.Name);
            Descriptor = new CollectionDescriptor(Descriptor.ProjectDescriptor, newPath);
        }

        /// <inheritdoc />
        public Task LinkAssetsAsync(IEnumerable<IAsset> assets, CancellationToken cancellationToken)
        {
            return LinkAssetsAsync(assets.Select(AssetExtensions.SelectId), cancellationToken);
        }

        /// <inheritdoc />
        public Task LinkAssetsAsync(IEnumerable<AssetId> assetIds, CancellationToken cancellationToken)
        {
            return m_DataSource.AddAssetsToCollectionAsync(Descriptor, assetIds, cancellationToken);
        }

        /// <inheritdoc />
        public Task UnlinkAssetsAsync(IEnumerable<IAsset> assets, CancellationToken cancellationToken)
        {
            return UnlinkAssetsAsync(assets.Select(AssetExtensions.SelectId), cancellationToken);
        }

        /// <inheritdoc />
        public Task UnlinkAssetsAsync(IEnumerable<AssetId> assetIds, CancellationToken cancellationToken)
        {
            return m_DataSource.RemoveAssetsFromCollectionAsync(Descriptor, assetIds, cancellationToken);
        }

        /// <inheritdoc />
        public async Task MoveToNewPathAsync(CollectionPath newCollectionPath, CancellationToken cancellationToken)
        {
            await m_DataSource.MoveCollectionToNewPathAsync(Descriptor, newCollectionPath, cancellationToken);

            var newPath = CollectionPath.CombinePaths(newCollectionPath, Name);
            Descriptor = new CollectionDescriptor(Descriptor.ProjectDescriptor, newPath);
        }
    }
}
