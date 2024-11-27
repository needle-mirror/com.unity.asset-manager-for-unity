using System;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    static partial class EntityMapper
    {
        internal static AssetProjectEntity From(this IProjectData data, IAssetDataSource assetDataSource, OrganizationId organizationId)
        {
            return data.From(assetDataSource, new ProjectDescriptor(organizationId, data.Id));
        }

        internal static AssetProjectEntity From(this IProjectData data, IAssetDataSource assetDataSource, ProjectDescriptor projectDescriptor)
        {
            return new AssetProjectEntity(assetDataSource, projectDescriptor)
            {
                Name = data.Name,
                Metadata = data.Metadata,
                HasCollection = data.HasCollection
            };
        }
    }
}
