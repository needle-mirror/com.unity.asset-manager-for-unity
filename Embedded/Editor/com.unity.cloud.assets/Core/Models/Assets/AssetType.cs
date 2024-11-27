using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// Asset's type accepted values.
    /// </summary>
    [DataContract]
enum AssetType
    {
        [EnumMember(Value = "2D Asset")]
        Asset_2D,
        [EnumMember(Value = "3D Model")]
        Model_3D,
        [EnumMember(Value = "Audio")]
        Audio,
        [EnumMember(Value = "Material")]
        Material,
        [EnumMember(Value = "Other")]
        Other,
        [EnumMember(Value = "Script")]
        Script,
        [EnumMember(Value = "Video")]
        Video,
        [EnumMember(Value = "Unity Editor")]
        Unity_Editor,
    }

    static class AssetTypeExtensions
    {
        /// <summary>
        /// Returns the string value of the AssetType.
        /// </summary>
        /// <param name="assetType"></param>
        /// <returns></returns>
        public static string GetValueAsString(this AssetType assetType)
        {
            return assetType switch
            {
                AssetType.Asset_2D => "2D Asset",
                AssetType.Model_3D => "3D Model",
                AssetType.Audio => "Audio",
                AssetType.Material => "Material",
                AssetType.Other => "Other",
                AssetType.Script => "Script",
                AssetType.Video => "Video",
                AssetType.Unity_Editor => "Unity Editor",
                _ => string.Empty
            };
        }

        /// <summary>
        /// Returns the AssetType from the string value.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="assetType"></param>
        /// <returns></returns>
        public static bool TryGetAssetTypeFromString(this string value, out AssetType assetType)
        {
            assetType = AssetType.Other;

            if (Enum.TryParse(value, out assetType)) return true;

            switch (value.Trim())
            {
                case var s when s.OrdinalEquals("2D Asset") || s.OrdinalEquals("2D") || s.OrdinalEquals("Asset"):
                    assetType = AssetType.Asset_2D;
                    break;
                case var s when s.OrdinalEquals("3D Model") || s.OrdinalEquals("3D") || s.OrdinalEquals("Model"):
                    assetType = AssetType.Model_3D;
                    break;
                case var s when s.OrdinalEquals("Audio"):
                    assetType = AssetType.Audio;
                    break;
                case var s when s.OrdinalEquals("Material"):
                    assetType = AssetType.Material;
                    break;
                case var s when s.OrdinalEquals("Other"):
                    assetType = AssetType.Other;
                    break;
                case var s when s.OrdinalEquals("Script"):
                    assetType = AssetType.Script;
                    break;
                case var s when s.OrdinalEquals("Video"):
                    assetType = AssetType.Video;
                    break;
                case var s when s.OrdinalEquals("Unity Editor") || s.OrdinalEquals("Unity"):
                    assetType = AssetType.Unity_Editor;
                    break;
                default:
                    return false;
            }

            return true;
        }

        static bool OrdinalEquals(this string value, string other)
        {
            return value.Equals(other, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns a list of all the AssetType values.
        /// </summary>
        /// <returns></returns>
        public static List<string> AssetTypeList()
        {
            var assetTypes = new List<string>();

            foreach (var value in Enum.GetValues(typeof(AssetType)))
            {
                assetTypes.Add(((AssetType)value).GetValueAsString());
            }

            return assetTypes;
        }
    }
}
