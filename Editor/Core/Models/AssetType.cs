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
        UnityEditor = 7,
        Animation = 8,
        AssemblyDefinition = 9,
        Asset = 10,
        AudioMixer = 11,
        Configuration = 12,
        Document = 13,
        Environment = 14,
        Font = 15,
        PhysicsMaterial = 16,
        Playable = 17,
        Prefab = 18,
        Scene = 19,
        Shader = 20,
        ShaderGraph = 21,
        UnityPackage = 22,
        UnityScene = 23,
        VisualEffect = 24,
        Image = 25
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
                AssetType.Animation => "Animation",
                AssetType.AssemblyDefinition => "Assembly Definition",
                AssetType.Asset => "Asset",
                AssetType.AudioMixer => "Audio Mixer",
                AssetType.Configuration => "Configuration",
                AssetType.Document => "Document",
                AssetType.Environment => "Environment",
                AssetType.Font => "Font",
                AssetType.PhysicsMaterial => "Physics Material",
                AssetType.Playable => "Playable",
                AssetType.Prefab => "Prefab",
                AssetType.Scene => "Scene",
                AssetType.Shader => "Shader",
                AssetType.ShaderGraph => "Shader Graph",
                AssetType.UnityPackage => "Unity Package",
                AssetType.UnityScene => "Unity Scene",
                AssetType.VisualEffect => "Visual Effect",
                AssetType.Image => "Image",
                _ => string.Empty
            };
        }

        internal static AssetType ConvertUnityAssetTypeToAssetType(this UnityAssetType unityAssetType)
        {
            return unityAssetType switch
            {
                UnityAssetType.AnimationClip => AssetType.Animation,
                UnityAssetType.AudioClip => AssetType.Audio,
                UnityAssetType.AudioMixer => AssetType.AudioMixer,
                UnityAssetType.Font => AssetType.Font,
                UnityAssetType.Material => AssetType.Material,
                UnityAssetType.Mesh => AssetType.Model3D,
                UnityAssetType.PhysicsMaterial => AssetType.PhysicsMaterial,
                UnityAssetType.Prefab => AssetType.Prefab,
                UnityAssetType.Scene => AssetType.UnityScene,
                UnityAssetType.Script => AssetType.Script,
                UnityAssetType.Shader => AssetType.Shader,
                UnityAssetType.Texture => AssetType.Asset2D,
                UnityAssetType.VisualEffect => AssetType.VisualEffect,
                UnityAssetType.AssemblyDefinition => AssetType.AssemblyDefinition,
                UnityAssetType.Asset => AssetType.Asset,
                UnityAssetType.Configuration => AssetType.Configuration,
                UnityAssetType.Document => AssetType.Document,
                UnityAssetType.Environment => AssetType.Environment,
                UnityAssetType.Image => AssetType.Image,
                UnityAssetType.Playable => AssetType.Playable,
                UnityAssetType.ShaderGraph => AssetType.ShaderGraph,
                UnityAssetType.UnityPackage => AssetType.UnityPackage,

                _ => AssetType.Other
            };
        }
    }
}
