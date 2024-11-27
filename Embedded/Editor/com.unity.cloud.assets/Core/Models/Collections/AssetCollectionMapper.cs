using System;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    static partial class EntityMapper
    {
        internal static AssetCollection From(this IAssetCollectionData data, IAssetDataSource dataSource, ProjectDescriptor projectDescriptor)
        {
            return data.From(dataSource, new CollectionDescriptor(projectDescriptor, data.GetFullCollectionPath()));
        }

        internal static AssetCollection From(this IAssetCollectionData data, IAssetDataSource dataSource, CollectionDescriptor collectionDescriptor)
        {
            return new AssetCollection(dataSource, collectionDescriptor)
            {
                Description = data.Description,
            };
        }

        internal static IAssetCollectionData From(this IAssetCollection assetCollection)
        {
            return new AssetCollectionData(assetCollection.Name, assetCollection.ParentPath)
            {
                Description = assetCollection.Description,
            };
        }

        internal static IAssetCollectionData From(this IAssetCollectionUpdate assetCollectionUpdate)
        {
            Validate("not null", assetCollectionUpdate.Description);

            return new AssetCollectionData(assetCollectionUpdate.Name)
            {
                Description = assetCollectionUpdate.Description,
            };
        }

        internal static void Validate(this IAssetCollectionCreation assetCollectionCreation) => Validate(assetCollectionCreation.Name, assetCollectionCreation.Description);

        static void Validate(string name, string description)
        {
#pragma warning disable S3928 // Parameter names used into ArgumentException constructors should match an existing one
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentNullException(nameof(IAssetCollectionCreation.Name), "The name of the collection cannot be null or empty.");
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentNullException(nameof(IAssetCollectionCreation.Description), "The description of the collection cannot be null or empty.");
            }
#pragma warning restore S3928 // Parameter names used into ArgumentException constructors should match an existing one
        }
    }
}
