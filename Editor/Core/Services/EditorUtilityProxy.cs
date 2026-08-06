using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.AssetManager.Core.Editor
{
    interface IEditorUtilityProxy : IService
    {
        bool IsDirty(string path);

        void ClearDirty(Object obj);

        void UnloadUnusedAssetsImmediate();

        void RevealInFinder(string path);

        void DisplayProgressBar(string title, string info, float progress);

        void ClearProgressBar();

        bool DisplayDialog(string title, string message, string ok);

        bool DisplayDialog(string title, string message, string ok, string cancel);

        int DisplayDialogComplex(string title, string message, string ok, string cancel, string alt);

        string OpenFolderPanel(string title, string folder, string defaultName);
    }

    [Serializable]
    [ExcludeFromCoverage]
    class EditorUtilityProxy : BaseService<IEditorUtilityProxy>, IEditorUtilityProxy
    {
        Type m_ShaderGraphEditorWindowType;
        PropertyInfo m_ShaderGraphAssetGuidProperty;

        public bool IsDirty(string path)
        {
            UnloadUnusedAssetsImmediate();

            // Check if the path is a loaded SceneAsset
            var scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid())
            {
                return scene.isDirty;
            }

            // If an asset is not already loaded it shouldn't be dirty.
            // However, the act of loading the asset could set it dirty; so only load when necessary to check.
            if (!AssetDatabase.IsMainAssetAtPathLoaded(path)) return false;

            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);

            if (asset == null) return false;

            // Shaders usually remain loaded in memory, check if an editor window is open for it
            // Otherwise consider the shader not dirty.
            if (asset is Shader)
            {
                return IsShaderGraphOpen(path);
            }

            return EditorUtility.IsDirty(asset);
        }

        public void ClearDirty(Object obj) => EditorUtility.ClearDirty(obj);

        public void UnloadUnusedAssetsImmediate() => EditorUtility.UnloadUnusedAssetsImmediate();

        public void RevealInFinder(string path) => EditorUtility.RevealInFinder(path);

        public void DisplayProgressBar(string title, string info, float progress) =>
            EditorUtility.DisplayProgressBar(title, info, progress);

        public void ClearProgressBar() => EditorUtility.ClearProgressBar();

        public bool DisplayDialog(string title, string message, string ok) =>
            EditorUtility.DisplayDialog(title, message, ok);

        public bool DisplayDialog(string title, string message, string ok, string cancel) =>
            EditorUtility.DisplayDialog(title, message, ok, cancel);

        public int DisplayDialogComplex(string title, string message, string ok, string cancel, string alt) =>
            EditorUtility.DisplayDialogComplex(title, message, ok, cancel, alt);

        public string OpenFolderPanel(string title, string folder, string defaultName) =>
            EditorUtility.OpenFolderPanel(title, folder, defaultName);

        bool IsShaderGraphOpen(string path)
        {
            LoadShaderGraphAssembly();

            if (m_ShaderGraphAssetGuidProperty == null) return false;

            var shaderGraphWindows = Resources.FindObjectsOfTypeAll(m_ShaderGraphEditorWindowType);
            foreach (var shaderGraphWindow in shaderGraphWindows)
            {
                var guid = m_ShaderGraphAssetGuidProperty.GetValue(shaderGraphWindow) as string;
                if (path == AssetDatabase.GUIDToAssetPath(guid))
                {
                    return (shaderGraphWindow as EditorWindow)?.hasUnsavedChanges ?? true;
                }
            }

            return false;
        }

        void LoadShaderGraphAssembly()
        {
            if (m_ShaderGraphEditorWindowType != null && m_ShaderGraphAssetGuidProperty != null) return;

            // Reset values
            m_ShaderGraphAssetGuidProperty = null;
            m_ShaderGraphEditorWindowType = null;

            const string assemblyName = "Unity.ShaderGraph.Editor";
            const string typeName = "UnityEditor.ShaderGraph.Drawing.MaterialGraphEditWindow";
            const string propertyName = "selectedGuid";

            try
            {
                var shaderGraphEditorAssembly = Assembly.Load(assemblyName);
                m_ShaderGraphEditorWindowType = shaderGraphEditorAssembly.GetType(typeName);

                if (m_ShaderGraphEditorWindowType != null)
                {
                    m_ShaderGraphAssetGuidProperty = m_ShaderGraphEditorWindowType.GetProperty(propertyName,
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                }
            }
            catch (FileNotFoundException)
            {
                // Ignore - assembly could not be loaded.
            }
        }
    }
}
