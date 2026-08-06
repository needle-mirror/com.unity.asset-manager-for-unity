using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if UNITY_6000_5_OR_NEWER
using GUID = UnityEngine.GUID;
#endif

namespace Unity.AssetManager.Core.Editor
{
    interface IAssetDatabaseProxy : IService
    {
        event Action<string[] /*importedAssets*/, string[] /*deletedAssets*/, string[] /*movedAssets*/, string[] /*movedFromAssetPaths*/> PostprocessAllAssets;

        string[] FindAssets(string filter, string[] searchInFolders);

        bool AssetPathExists(string path);
        bool DeleteAssets(string[] paths, List<string> outFailedPaths);
        string AssetPathToGuid(string assetPath);
        string GuidToAssetPath(string guid);
        GUID GuidFromAssetPath(string path);
        bool IsValidFolder(string path);
        Type GetMainAssetTypeAtPath(string path);
        void Refresh();
        string GetAssetPath(Object obj);
        string GetTextMetaFilePathFromAssetPath(string fileName);
        string[] GetDependencies(string assetPath, bool recursive);
        void SaveAssetIfDirty(string assetPath);
        void ImportAsset(string assetPath);
        string[] GetLabels(GUID guid);
        void StartAssetEditing();
        void StopAssetEditing();
        Object LoadAssetAtPath(string assetPath);
        Object LoadAssetAtPath(string assetPath, Type type);

        bool PingAssetByGuid(string guid);
        bool CanPingAssetByGuid(string guid);
        IEnumerable<string> GetAssetsInFolder(string folder);
        void ReleaseCachedFileHandles();
        string MoveAsset(string oldPath, string newPath);
        string CreateFolder(string parentFolder, string newFolderName);
    }

    [Serializable]
    [ExcludeFromCoverage]
    class AssetDatabaseProxy : BaseService<IAssetDatabaseProxy>, IAssetDatabaseProxy
    {
        class AssetPostprocessor : UnityEditor.AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                if (ServicesContainer.instance.Resolve<IAssetDatabaseProxy>() is AssetDatabaseProxy assetDatabaseProxy)
                {
                    assetDatabaseProxy.PostprocessAllAssets?.Invoke(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
                }
            }
        }

        public event Action<string[] /*importedAssets*/, string[] /*deletedAssets*/, string[] /*movedAssets*/, string[] /*movedFromAssetPaths*/> PostprocessAllAssets = delegate {};

        // Wrapper AssetDatabase methods

        public bool AssetPathExists(string assetPath) =>
            !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath, AssetPathToGUIDOptions.OnlyExistingAssets));

        public string[] FindAssets(string filter, string[] searchInFolders) => AssetDatabase.FindAssets(filter, searchInFolders);

        public bool DeleteAssets(string[] paths, List<string> outFailedPaths) => AssetDatabase.DeleteAssets(paths, outFailedPaths);

        public string AssetPathToGuid(string assetPath) => AssetDatabase.AssetPathToGUID(assetPath);

        public string GuidToAssetPath(string guid) => AssetDatabase.GUIDToAssetPath(guid);

        public GUID GuidFromAssetPath(string guid) => AssetDatabase.GUIDFromAssetPath(guid);

        public bool IsValidFolder(string path) => AssetDatabase.IsValidFolder(path);

        public Type GetMainAssetTypeAtPath(string path) => AssetDatabase.GetMainAssetTypeAtPath(path);

        public void Refresh() => AssetDatabase.Refresh();

        public string GetAssetPath(Object obj) => AssetDatabase.GetAssetPath(obj);

        public string GetTextMetaFilePathFromAssetPath(string fileName) => AssetDatabase.GetTextMetaFilePathFromAssetPath(fileName);

        public string[] GetDependencies(string assetPath, bool recursive) => AssetDatabase.GetDependencies(assetPath, recursive);

        public void SaveAssetIfDirty(string assetPath)
        {
            // Check if the path is a loaded SceneAsset
            var scene = SceneManager.GetSceneByPath(assetPath);
            if (scene.IsValid())
            {
                if (scene.isDirty)
                {
                    EditorSceneManager.SaveScene(scene);
                }
            }
            else if (AssetDatabase.IsMainAssetAtPathLoaded(assetPath))
            {
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.GUIDFromAssetPath(assetPath));
            }
        }

        public void ImportAsset(string assetPath) => AssetDatabase.ImportAsset(assetPath);

        public string[] GetLabels(GUID guid) => AssetDatabase.GetLabels(guid);

        public void StartAssetEditing() => AssetDatabase.StartAssetEditing();

        public void StopAssetEditing() => AssetDatabase.StopAssetEditing();

        public Object LoadAssetAtPath(string assetPath) => AssetDatabase.LoadAssetAtPath<Object>(assetPath);

        public Object LoadAssetAtPath(string assetPath, Type type) => AssetDatabase.LoadAssetAtPath(assetPath, type);

        // End of wrapper AssetDatabase methods

        public bool PingAssetByGuid(string guid)
        {
            var assetObject = LoadAssetAtPath(GuidToAssetPath(guid));

            if (assetObject != null)
            {
                EditorGUIUtility.PingObject(assetObject);
                return true;
            }

            return false;
        }

        public bool CanPingAssetByGuid(string guid)
        {
            var path = GuidToAssetPath(guid);
            return !string.IsNullOrEmpty(path) && AssetPathExists(path);
        }

        public IEnumerable<string> GetAssetsInFolder(string folder)
        {
            var subAssetGuids = FindAssets(string.Empty, new[] { folder });
            foreach (var subAssetGuid in subAssetGuids)
            {
                var path = GuidToAssetPath(subAssetGuid);
                if (!IsValidFolder(path))
                {
                    yield return subAssetGuid;
                }
            }
        }

        public void ReleaseCachedFileHandles()
        {
            AssetDatabase.ReleaseCachedFileHandles();
        }

        public string MoveAsset(string oldPath, string newPath) => AssetDatabase.MoveAsset(oldPath, newPath);
        public string CreateFolder(string parentFolder, string newFolderName) => AssetDatabase.CreateFolder(parentFolder, newFolderName);
    }
}
