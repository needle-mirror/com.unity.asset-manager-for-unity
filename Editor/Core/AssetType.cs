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
            return assetType.ConvertAssetTypeToCloudAssetType().GetValueAsString();
        }

        internal static Cloud.Assets.AssetType ConvertAssetTypeToCloudAssetType(this AssetType assetType)
        {
            return assetType switch
            {
                AssetType.Asset2D => Cloud.Assets.AssetType.Asset_2D,
                AssetType.Model3D => Cloud.Assets.AssetType.Model_3D,
                AssetType.Audio => Cloud.Assets.AssetType.Audio,
                AssetType.Material => Cloud.Assets.AssetType.Material,
                AssetType.Script => Cloud.Assets.AssetType.Script,
                AssetType.Video => Cloud.Assets.AssetType.Video,
                _ => Cloud.Assets.AssetType.Other
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

        internal static AssetType ConvertUnityAssetTypeToAssetType(this UnityAssetType unityAssetType)
        {
            return unityAssetType switch
            {
                UnityAssetType.AnimationClip => AssetType.Other,
                UnityAssetType.AudioClip => AssetType.Audio,
                UnityAssetType.AudioMixer => AssetType.Audio,
                UnityAssetType.Font => AssetType.Other,
                UnityAssetType.Material => AssetType.Material,
                UnityAssetType.Mesh => AssetType.Model3D,
                UnityAssetType.PhysicMaterial => AssetType.Other,
                UnityAssetType.Prefab => AssetType.Model3D,
                UnityAssetType.Scene => AssetType.Other,
                UnityAssetType.Script => AssetType.Other,
                UnityAssetType.Shader => AssetType.Other,
                UnityAssetType.Texture => AssetType.Asset2D,
                UnityAssetType.VisualEffect => AssetType.Other,
                _ => AssetType.Other
            };
        }
    }
}
