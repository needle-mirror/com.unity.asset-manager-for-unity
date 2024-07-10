using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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
        public readonly HashSet<string> Extensions;
        public readonly UnityAssetType Type;

        readonly IconSource m_IconSource;
        readonly string m_IconStr;

        public UnityTypeDescriptor(UnityAssetType type, params string[] ext)
        {
            Type = type;
            Extensions = new HashSet<string>(ext);
            m_IconSource = IconSource.Default;
            m_IconStr = string.Empty;
        }

        public UnityTypeDescriptor(UnityAssetType type, IconSource iconSource, string iconStr, params string[] ext)
        {
            Type = type;
            Extensions = new HashSet<string>(ext);
            m_IconSource = iconSource;
            m_IconStr = iconStr;
        }

        public Texture2D GetIcon()
        {
            switch (m_IconSource)
            {
                case IconSource.Typename:
                    return AssetDataTypeHelper.GetIconFromType(m_IconStr);
                case IconSource.Resource:
                    return AssetDataTypeHelper.GetIconFromResource(m_IconStr);
                case IconSource.TextureName:
                    return AssetDataTypeHelper.GetIconFromTextureName(m_IconStr);
                default:
                    return InternalEditorUtility.GetIconForFile(Extensions.FirstOrDefault());
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
            new UnityTypeDescriptor(UnityAssetType.Other, IconSource.TextureName, "SpeedTreeImporter Icon", ".st"),
            new UnityTypeDescriptor(UnityAssetType.Mesh, ".3df", ".3dm", ".3dmf", ".3ds", ".3dv", ".3dx",
                ".blend", ".c4d", ".fbx", ".lwo", ".lws", ".ma", ".max", ".mb", ".mesh", ".obj", ".vrl", ".wrl",
                ".wrz"),
            new UnityTypeDescriptor(UnityAssetType.Mesh, IconSource.Resource,
                "Packages/com.unity.cloud.gltfast/Editor/UI/gltf-icon-bug.png", ".glb", ".gltf"),
            new UnityTypeDescriptor(UnityAssetType.Material, ".mat"),
            new UnityTypeDescriptor(UnityAssetType.AnimationClip, IconSource.TextureName, "d_AnimationClip Icon",
                ".anim"),
            new UnityTypeDescriptor(UnityAssetType.AudioClip, ".aac", ".aif", ".aiff", ".au", ".flac", ".mid",
                ".midi", ".mp3", ".mpa", ".ogg", ".ra", ".ram", ".wav", ".wave", ".wma"),
            new UnityTypeDescriptor(UnityAssetType.AudioMixer, ".mixer"),
            new UnityTypeDescriptor(UnityAssetType.Font, ".fnt", ".fon", ".otf", ".ttf", ".ttc"),
            new UnityTypeDescriptor(UnityAssetType.PhysicMaterial, ".physicmaterial"),
            new UnityTypeDescriptor(UnityAssetType.Script, ".cs"),
            new UnityTypeDescriptor(UnityAssetType.Shader, ".shader"),
            new UnityTypeDescriptor(UnityAssetType.Shader, IconSource.Resource,
                "Packages/com.unity.shadergraph/Editor/Resources/Icons/sg_graph_icon.png", ".shadergraph"),
            new UnityTypeDescriptor(UnityAssetType.Shader, IconSource.Resource,
                "Packages/com.unity.shadergraph/Editor/Resources/Icons/sg_subgraph_icon.png", ".shadersubgraph"),
            new UnityTypeDescriptor(UnityAssetType.Texture, ".ai", ".apng", ".bmp", ".cdr", ".dib", ".eps",
                ".exif", ".exr", ".gif", ".hdr", ".ico", ".icon", ".j", ".j2c", ".j2k", ".jas", ".jiff", ".jng", ".jp2",
                ".jpc", ".jpe", ".jpeg", ".jpf", ".jpg", ".jpw", ".jpx", ".jtf", ".mac", ".omf", ".png", ".psd", ".qif",
                ".qti", ".qtif", ".tex", ".tfw", ".tga", ".tif", ".tiff", ".wmf"),
            new UnityTypeDescriptor(UnityAssetType.VisualEffect, IconSource.Typename,
                "UnityEngine.VFX.VisualEffectAsset", ".vfx"),
            new UnityTypeDescriptor(UnityAssetType.Other, IconSource.Typename, "UnityEngine.Timeline.TimelineAsset",
                ".playable"),
            new UnityTypeDescriptor(UnityAssetType.Other, IconSource.TextureName, "d_AnimatorController Icon",
                ".controller"),
            new UnityTypeDescriptor(UnityAssetType.Other, IconSource.TextureName, "d_SceneAsset Icon", ".unitypackage")
        };

        static Dictionary<string, UnityTypeDescriptor> s_ExtensionToUnityTypeDescriptor;

        static Texture2D DefaultIcon => InternalEditorUtility.GetIconForFile(string.Empty);

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
            {
                return null;
            }

            foreach (var unityTypeDescriptor in k_UnityTypeDescriptors)
            {
                foreach (var extension in assetExtensions)
                {
                    if (unityTypeDescriptor.Type == GetUnityAssetType(extension))
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

        public static UnityAssetType GetUnityAssetType(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return UnityAssetType.Other;
            }

            InitializeExtensionToUnityTypeDescriptor();

            if (s_ExtensionToUnityTypeDescriptor.TryGetValue(extension, out var descriptor))
            {
                return descriptor.Type;
            }

            return UnityAssetType.Other;
        }

        public static Regex GetRegexForExtensions(UnityAssetType type)
        {
            var pattern = string.Empty;
            var extensions = k_UnityTypeDescriptors.Find(x => x.Type == type)?.Extensions;

            if (extensions != null)
            {
                foreach (var extension in extensions)
                {
                    pattern += $"|{extension}";
                }
            }

            pattern = pattern[1..];

            return new Regex($".*({pattern})", RegexOptions.IgnoreCase);
        }

        static void InitializeExtensionToUnityTypeDescriptor()
        {
            if (s_ExtensionToUnityTypeDescriptor != null)
                return;

            s_ExtensionToUnityTypeDescriptor = new Dictionary<string, UnityTypeDescriptor>();
            foreach (var unityTypeDescriptor in k_UnityTypeDescriptors)
            {
                foreach (var ext in unityTypeDescriptor.Extensions)
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
            {
                return DefaultIcon;
            }

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
    }
}
