using System.Collections.Generic;

namespace Unity.Cloud.AssetsEmbedded
{
    interface IAssetCreation : IAssetInfo
    {
        /// <inheritdoc cref="IAsset.Type"/>
        AssetType Type { get; }

        /// <inheritdoc cref="IAsset.Metadata"/>
        Dictionary<string, MetadataValue> Metadata { get; }

        /// <summary>
        /// The collections to which the asset should be added.
        /// </summary>
        List<CollectionPath> Collections { get; }
    }
}
