using System;
using UnityEditor;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class UploadSettings
    {
        static readonly string k_UploadModePrefKey = "com.unity.asset-manager-for-unity.upload-mode";

        string SavedUploadMode
        {
            set => EditorPrefs.SetString(k_UploadModePrefKey, value);
            get => EditorPrefs.GetString(k_UploadModePrefKey, AssetUploadMode.Override.ToString());
        }

        AssetUploadMode m_AssetUploadMode = AssetUploadMode.None;

        public string OrganizationId;
        public string ProjectId;
        public string CollectionPath;

        public AssetUploadMode AssetUploadMode
        {
            get
            {
                if (m_AssetUploadMode == AssetUploadMode.None)
                {
                    m_AssetUploadMode = Enum.TryParse(SavedUploadMode, out AssetUploadMode mode)
                        ? mode
                        : AssetUploadMode.Override;
                }

                return m_AssetUploadMode;
            }
            set
            {
                m_AssetUploadMode = value;
                SavedUploadMode = value.ToString();
            }
        }
    }
}