using System;
using Unity.Cloud.Assets;

namespace Unity.AssetManager.Editor
{
    enum AssetType
    {
        Other = 0,
        Asset2D = 1,
        Model3D = 2,
        Audio = 3,
        Material = 4,
        Script = 5,
        Video = 6
    }

    static class AssetTypeExtensions
    {
        internal static string DisplayValue(this AssetType assetType)
        {
            return assetType switch
            {
                AssetType.Asset2D => Cloud.Assets.AssetType.Asset_2D.GetValueAsString(),
                AssetType.Model3D => Cloud.Assets.AssetType.Model_3D.GetValueAsString(),
                AssetType.Audio => Cloud.Assets.AssetType.Audio.GetValueAsString(),
                AssetType.Material => Cloud.Assets.AssetType.Material.GetValueAsString(),
                AssetType.Script => Cloud.Assets.AssetType.Script.GetValueAsString(),
                AssetType.Video => Cloud.Assets.AssetType.Video.GetValueAsString(),
                _ => Cloud.Assets.AssetType.Other.GetValueAsString()
            };
        }

        internal static AssetType ConvertCloudAssetTypeToAssetType(this Cloud.Assets.AssetType assetType)
        {
            return assetType switch
            {
                Cloud.Assets.AssetType.Asset_2D => AssetType.Asset2D,
                Cloud.Assets.AssetType.Model_3D => AssetType.Model3D,
                Cloud.Assets.AssetType.Audio => AssetType.Audio,
                Cloud.Assets.AssetType.Material => AssetType.Material,
                Cloud.Assets.AssetType.Script => AssetType.Script,
                Cloud.Assets.AssetType.Video => AssetType.Video,
                _ => AssetType.Other
            };
        }
    }
}
