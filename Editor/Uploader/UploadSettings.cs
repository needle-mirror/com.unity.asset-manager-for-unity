using System;
using UnityEditor;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class UploadSettings
    {
        static readonly string k_UploadModePrefKey = "com.unity.asset-manager-for-unity.upload-mode";
        static readonly string k_UploadDependencyModePrefKey = "com.unity.asset-manager-for-unity.upload-dependency-mode";
        static readonly string k_UploadFlattenFilePathsPrefKey = "com.unity.asset-manager-for-unity.upload-flatten-file-paths";

        public string OrganizationId;
        public string ProjectId;
        public string CollectionPath;

        public UploadAssetMode UploadMode
        {
            get => SavedUploadMode;
            set => SavedUploadMode = value;
        }

        public UploadDependencyMode DependencyMode
        {
            get => SavedDependencyMode;
            set => SavedDependencyMode = value;
        }

        public UploadFilePathMode FilePathMode
        {
            get => SavedFilePathMode;
            set => SavedFilePathMode = value;
        }

        UploadAssetMode SavedUploadMode
        {
            set => EditorPrefs.SetInt(k_UploadModePrefKey, (int)value);
            get => (UploadAssetMode)EditorPrefs.GetInt(k_UploadModePrefKey, (int)UploadAssetMode.Override);
        }

        UploadDependencyMode SavedDependencyMode
        {
            set => EditorPrefs.SetInt(k_UploadDependencyModePrefKey, (int)value);
            get => (UploadDependencyMode)EditorPrefs.GetInt(k_UploadDependencyModePrefKey, (int)UploadDependencyMode.Separate);
        }

        UploadFilePathMode SavedFilePathMode
        {
            set => EditorPrefs.SetInt(k_UploadFlattenFilePathsPrefKey, (int)value);
            get => (UploadFilePathMode)EditorPrefs.GetInt(k_UploadFlattenFilePathsPrefKey, (int)UploadFilePathMode.Full);
        }

        public static string GetUploadModeTooltip(UploadAssetMode mode)
        {
            return mode switch
            {
                UploadAssetMode.Duplicate => L10n.Tr("Uploads new assets and potentially duplicates without checking for existing matches"),

                UploadAssetMode.Override => L10n.Tr("Replaces and overrides any existing asset with the same id on the cloud"),

                UploadAssetMode.SkipExisting => L10n.Tr("Ignores and skips the upload if an asset with the same id already exists on the cloud"),

                _ => null
            };
        }

        public static string GetDependencyModeTooltip(UploadDependencyMode mode)
        {
            return mode switch
            {
                UploadDependencyMode.Ignore => L10n.Tr("Dependencies will be ignored and not uploaded"),

                UploadDependencyMode.Separate => L10n.Tr("Dependencies will be uploaded as separate Cloud Asset and the parent Cloud Asset will have a reference to them"),

                UploadDependencyMode.Embedded => L10n.Tr("Dependencies will have be added as files in the parent Cloud Asset"),

                _ => null
            };
        }

        public static string GetFilePathModeTooltip(UploadFilePathMode mode)
        {
            return mode switch
            {
                UploadFilePathMode.Full => L10n.Tr("Keep the original path relative to the project's Assets folder"),

                UploadFilePathMode.Compact => L10n.Tr("Reduce files nesting by removing common path parts"),

                UploadFilePathMode.Flatten => L10n.Tr("Flatten all files to the root of the asset and rename them in case of collision"),

                _ => null
            };
        }
    }
}