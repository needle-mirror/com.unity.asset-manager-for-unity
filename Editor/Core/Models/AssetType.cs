namespace Unity.AssetManager.Core.Editor
{
    enum AssetType
    {
        Other = 0,
        Asset2D = 1,
        Model3D = 2,
        Audio = 3,
        Material = 4,
        Script = 5,
        Video = 6,
        UnityEditor = 7
    }

    static class AssetTypeExtensions
    {
        public static string DisplayValue(this AssetType assetType)
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
                AssetType.UnityEditor => "Unity Editor",
                _ => string.Empty
            };
        }

        internal static AssetType ConvertUnityAssetTypeToAssetType(this UnityAssetType unityAssetType)
        {
            return unityAssetType switch
            {
                UnityAssetType.AnimationClip => AssetType.UnityEditor,
                UnityAssetType.AudioClip => AssetType.Audio,
                UnityAssetType.AudioMixer => AssetType.UnityEditor,
                UnityAssetType.Font => AssetType.Other,
                UnityAssetType.Material => AssetType.UnityEditor,
                UnityAssetType.Mesh => AssetType.Model3D,
                UnityAssetType.PhysicMaterial => AssetType.UnityEditor,
                UnityAssetType.Prefab => AssetType.UnityEditor,
                UnityAssetType.Scene => AssetType.UnityEditor,
                UnityAssetType.Script => AssetType.UnityEditor,
                UnityAssetType.Shader => AssetType.UnityEditor,
                UnityAssetType.Texture => AssetType.Asset2D,
                UnityAssetType.VisualEffect => AssetType.UnityEditor,
                _ => AssetType.Other
            };
        }
    }
}
