using System.IO;
using Unity.Cloud.AppLinkingEmbedded.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Cloud.AppLinkingEmbedded.Editor
{
    /// <summary>
    /// A <see cref="SettingsProvider"/> implementation for Unity Cloud settings.
    /// </summary>
    internal class UnityCloudPlayerSettingsProvider : SettingsProvider
    {
        const string k_ResourcesDirectory = "Assets/Unity Cloud/Resources/";
        static readonly string k_AssetPath = $"{k_ResourcesDirectory}{UnityCloudPlayerSettings.k_AssetName}.asset";

        UnityCloudPlayerSettingsEditor m_UnityCloudPlayerSettingsEditor;

        UnityCloudPlayerSettingsProvider(string path, SettingsScope scope = SettingsScope.User)
            : base(path, scope) {}

        /// <summary>
        /// Creates an instance of <see cref="UnityCloudPlayerSettingsProvider"/>.
        /// </summary>
        /// <returns>The created instance.</returns>
        [SettingsProvider]
        public static SettingsProvider CreateUnityCloudPlayerSettingsProvider()
        {
            return new UnityCloudPlayerSettingsProvider("Project/Services/Unity Cloud", SettingsScope.Project)
            {
                keywords = new[]
                {
                    nameof(UnityCloudPlayerSettings.AppId),
                }
            };
        }

        /// <inheritdoc/>
        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            var settings = Resources.Load<UnityCloudPlayerSettings>(UnityCloudPlayerSettings.k_AssetName);

            if (settings == null)
            {
                settings = UnityCloudPlayerSettings.Instance;

                if (!AssetDatabase.IsValidFolder(k_ResourcesDirectory))
                    Directory.CreateDirectory(k_ResourcesDirectory);

                AssetDatabase.CreateAsset(settings, k_AssetPath);
                AssetDatabase.SaveAssets();
            }

            m_UnityCloudPlayerSettingsEditor = UnityEditor.Editor.CreateEditor(settings) as UnityCloudPlayerSettingsEditor;
        }

        /// <inheritdoc/>
        public override void OnGUI(string searchContext)
        {
            base.OnGUI(searchContext);

            m_UnityCloudPlayerSettingsEditor.DrawGUI();
        }
    }
}
