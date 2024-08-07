using System;

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
            return assetType.GetValueAsString();
        }
        
        static string GetValueAsString(this AssetType assetType)
        {
            return assetType switch
            {
                AssetType.Asset2D => "2D Asset",
                AssetType.Model3D => "3D Model",
                AssetType.Audio => "Audio",
                AssetType.Material => "Material",
                AssetType.Other => "Other",
                AssetType.Script => "Script",
                AssetType.Video => "Video",
                _ => string.Empty
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
