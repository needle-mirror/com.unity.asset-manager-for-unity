using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    public enum UnityAssetType
    {
        AnimationClip,
        AudioClip,
        AudioMixer,
        Font,
        Material,
        Mesh,
        PhysicMaterial,
        Prefab,
        Scene,
        Script,
        Shader,
        Texture,
        VisualEffect,
        Other
    }

    enum IconSource
    {
        Default,
        Typename,
        Resource,
        TextureName
    }

    class UnityTypeDescriptor
    {
        public UnityAssetType type;
        public HashSet<string> extensions;
        public IconSource iconSource;
        public string iconStr;

        public UnityTypeDescriptor(UnityAssetType type, params string[] ext)
        {
            this.type = type;
            extensions = new HashSet<string>(ext);
            iconSource = IconSource.Default;
            iconStr = string.Empty;
        }

        public UnityTypeDescriptor(UnityAssetType type, IconSource iconSource, string iconStr, params string[] ext)
        {
            this.type = type;
            extensions = new HashSet<string>(ext);
            this.iconSource = iconSource;
            this.iconStr = iconStr;
        }

        public Texture2D GetIcon()
        {
            switch (iconSource)
            {
                case IconSource.Typename:
                    return AssetDataTypeHelper.GetIconFromType(iconStr);
                case IconSource.Resource:
                    return AssetDataTypeHelper.GetIconFromResource(iconStr);
                case IconSource.TextureName:
                    return AssetDataTypeHelper.GetIconFromTextureName(iconStr);
                default:
                    return InternalEditorUtility.GetIconForFile(extensions.FirstOrDefault());
            }
        }
    }

    static class AssetDataTypeHelper
    {
        // Order is important!
        static readonly List<UnityTypeDescriptor> k_UnityTypeDescriptors = new()
        {
            new UnityTypeDescriptor(UnityAssetType.Scene, ".unity"),
            new UnityTypeDescriptor(UnityAssetType.Prefab, ".prefab"),
            new UnityTypeDescriptor(UnityAssetType.Mesh, ".3df", ".3dm", ".3dmf", ".3ds", ".3dv", ".3dx",
                ".blend", ".c4d", ".fbx", ".lwo", ".lws", ".ma", ".max", ".mb", ".mesh", ".obj", ".vrl", ".wrl",
                ".wrz"),
            new UnityTypeDescriptor(UnityAssetType.Mesh, IconSource.Resource, "Packages/com.unity.cloud.gltfast/Editor/UI/gltf-icon-bug.png", ".glb", ".gltf"),
            new UnityTypeDescriptor(UnityAssetType.Material, ".mat"),
            new UnityTypeDescriptor(UnityAssetType.AnimationClip, IconSource.TextureName, "d_AnimationClip Icon", ".anim"),
            new UnityTypeDescriptor(UnityAssetType.AudioClip, ".aac", ".aif", ".aiff", ".au", ".flac", ".mid",
                ".midi", ".mp3", ".mpa", ".ogg", ".ra", ".ram", ".wav", ".wave", ".wma"),
            new UnityTypeDescriptor(UnityAssetType.AudioMixer, ".mixer"),
            new UnityTypeDescriptor(UnityAssetType.Font, ".fnt", ".fon", ".otf", ".ttf",".ttc"),
            new UnityTypeDescriptor(UnityAssetType.PhysicMaterial, ".physicmaterial"),
            new UnityTypeDescriptor(UnityAssetType.Script, ".cs"),
            new UnityTypeDescriptor(UnityAssetType.Shader, ".shader"),
            new UnityTypeDescriptor(UnityAssetType.Shader, IconSource.Resource, "Packages/com.unity.shadergraph/Editor/Resources/Icons/sg_graph_icon.png", ".shadergraph"),
            new UnityTypeDescriptor(UnityAssetType.Texture, ".ai", ".apng", ".bmp", ".cdr", ".dib", ".eps",
                ".exif", ".exr", ".gif", ".hdr", ".ico", ".icon", ".j", ".j2c", ".j2k", ".jas", ".jiff", ".jng", ".jp2",
                ".jpc", ".jpe", ".jpeg", ".jpf", ".jpg", ".jpw", ".jpx", ".jtf", ".mac", ".omf", ".png", ".psd", ".qif",
                ".qti", ".qtif", ".tex", ".tfw", ".tga", ".tif", ".tiff", ".wmf"),
            new UnityTypeDescriptor(UnityAssetType.VisualEffect, IconSource.Typename, "UnityEngine.VFX.VisualEffectAsset", ".vfx"),
            new UnityTypeDescriptor(UnityAssetType.Other, IconSource.Typename, "UnityEngine.Timeline.TimelineAsset", ".playable"),
            new UnityTypeDescriptor(UnityAssetType.Other, IconSource.TextureName,"d_AnimatorController Icon", ".controller"),
            new UnityTypeDescriptor(UnityAssetType.Other, IconSource.TextureName, "d_SceneAsset Icon", ".unitypackage")
        };

        static Dictionary<string, UnityTypeDescriptor> s_ExtensionToUnityTypeDescriptor;

        public static string GetAssetPrimaryExtension(IEnumerable<string> extensions)
        {
            var assetExtensions = new HashSet<string>();

            foreach (var extension in extensions)
            {
                if (string.IsNullOrEmpty(extension))
                    continue;

                var ext = extension.ToLower();

                if (ext == MetafilesHelper.MetaFileExtension)
                    continue;

                if (AssetDataDependencyHelper.IsASystemFile(ext))
                    continue;

                assetExtensions.Add(ext);
            }

            if (assetExtensions.Count == 0)
                return null;

            foreach (var unityTypeDescriptor in k_UnityTypeDescriptors)
            {
                foreach (var extension in assetExtensions)
                {
                    if (unityTypeDescriptor.type == GetUnityAssetType(extension))
                    {
                        return extension;
                    }
                }
            }

            return assetExtensions.FirstOrDefault();
        }

        public static Texture2D GetIconForExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return DefaultIcon;
            }

            InitializeExtensionToUnityTypeDescriptor();

            if (s_ExtensionToUnityTypeDescriptor.TryGetValue(extension, out var descriptor))
            {
                return descriptor.GetIcon();
            }

            return InternalEditorUtility.GetIconForFile(extension);
        }

        public static Texture2D GetIconForFile(string path)
        {
            return GetIconForExtension(string.IsNullOrEmpty(path) ? null : Path.GetExtension(path));
        }

        public static UnityAssetType GetUnityAssetType(string extension)
        {
            InitializeExtensionToUnityTypeDescriptor();

            if (s_ExtensionToUnityTypeDescriptor.TryGetValue(extension, out var descriptor))
            {
                return descriptor.type;
            }

            return UnityAssetType.Other;
        }

        static void InitializeExtensionToUnityTypeDescriptor()
        {
            if (s_ExtensionToUnityTypeDescriptor != null)
                return;

            s_ExtensionToUnityTypeDescriptor = new Dictionary<string, UnityTypeDescriptor>();
            foreach (var unityTypeDescriptor in k_UnityTypeDescriptors)
            {
                foreach (var ext in unityTypeDescriptor.extensions)
                {
                    s_ExtensionToUnityTypeDescriptor[ext] = unityTypeDescriptor;
                }
            }
        }

        static MethodInfo FindTextureByType()
        {
            return Type.GetType("UnityEditor.EditorGUIUtility,UnityEditor.dll")
                ?.GetMethod("FindTexture", BindingFlags.Static | BindingFlags.NonPublic);
        }

        static Type GetTypeInAnyAssembly(string typeName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                var type = assembly.GetType(typeName);

                if (type == null)
                    continue;

                return type;
            }

            return null;
        }

        internal static Texture2D GetIconFromType(string typeName)
        {
            var type = GetTypeInAnyAssembly(typeName);
            return GetIconFromType(type);
        }

        static Texture2D GetIconFromType(Type type)
        {
            if (type == null)
                return DefaultIcon;

            return FindTextureByType().Invoke(null, new object[] { type }) as Texture2D;
        }

        internal static Texture2D GetIconFromResource(string resourceName)
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(resourceName);
            return icon == null ? DefaultIcon : icon;
        }

        internal static Texture2D GetIconFromTextureName(string textureName)
        {
            if (!EditorGUIUtility.isProSkin && textureName.StartsWith("d_"))
            {
                textureName = textureName[2..];
            }

            var texture = EditorGUIUtility.IconContent(textureName).image as Texture2D;
            return texture != null ? texture : DefaultIcon;
        }

        static Texture2D DefaultIcon => InternalEditorUtility.GetIconForFile(string.Empty);
    }
}
