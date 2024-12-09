using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Cloud.AppLinkingEmbedded.Runtime;
using UnityEditor;
using UnityEngine;

namespace Unity.Cloud.AppLinkingEmbedded.Editor
{
    /// <summary>
    /// A custom editor for <see cref="UnityCloudPlayerSettings"/>.
    /// </summary>
    [CustomEditor(typeof(UnityCloudPlayerSettings))]
    internal class UnityCloudPlayerSettingsEditor : UnityEditor.Editor
    {

        [ReadOnly] SerializedProperty m_AppIdProperty;
        SerializedProperty m_AppNamespaceProperty;

        void OnEnable()
        {
            m_AppIdProperty = serializedObject.FindProperty(nameof(UnityCloudPlayerSettings.AppId));
            m_AppNamespaceProperty = serializedObject.FindProperty(nameof(UnityCloudPlayerSettings.AppNamespace));
        }

        void Awake()
        {
            EditorUtility.SetDirty(UnityCloudPlayerSettings.Instance);
        }

        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            DrawGUI();
        }

        /// <summary>
        /// Draws the Editor GUI.
        /// </summary>
        public void DrawGUI()
        {
            serializedObject.Update();

            ShowCurrentCloudPlayerSettingsUI();


            serializedObject.ApplyModifiedProperties();
        }

        void ShowCurrentCloudPlayerSettingsUI()
        {
            if (m_AppIdProperty.stringValue != CloudProjectSettings.projectId)
            {
                m_AppIdProperty.stringValue = CloudProjectSettings.projectId;
                UnityCloudPlayerSettings.Instance.AppId = CloudProjectSettings.projectId;
            }

            var infoMessage =
"Creates a unique App Namespace to intercept invocation events coming from the Operating System in your Unity Cloud app. Intercepting invocation events enables the activation of an app from either an idle or a shutdown state.";

            EditorGUILayout.HelpBox(infoMessage, MessageType.Info);

            GUILayout.Space(10);

            EditorGUILayout.PropertyField(m_AppNamespaceProperty);
            if (EditorGUI.EndChangeCheck())
            {
                var newValue = SanitizeNamespace(m_AppNamespaceProperty.stringValue);
                if (!newValue.Equals(m_AppNamespaceProperty.stringValue))
                {
                    m_AppNamespaceProperty.stringValue = newValue;
                    UnityCloudPlayerSettings.Instance.AppNamespace = newValue;
                }
            }
        }

        string SanitizeNamespace(string stringValue)
        {
            stringValue = Regex.Replace(stringValue, "\\s+", "", RegexOptions.None, TimeSpan.FromSeconds(1));
            if (stringValue[^1].Equals('.'))
            {
                stringValue = stringValue.TrimEnd('.');
            }
            return stringValue;
        }
    }


    /// <summary>
    /// Draws a property as read-only in the GUI.
    /// </summary>
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    internal class ReadOnlyDrawer : PropertyDrawer
    {
        /// <summary>
        /// Gets the height of the property.
        /// </summary>
        /// <param name="property">The property to draw.</param>
        /// <param name="label">The content label.</param>
        /// <returns></returns>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        /// <inheritdoc/>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = true;
        }
    }
}
