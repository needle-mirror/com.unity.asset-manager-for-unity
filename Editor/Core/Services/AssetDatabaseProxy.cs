using System;
using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Unity.AssetManager.Core.Editor
{
    interface IAssetDatabaseProxy : IService
    {
        event Action<string[] /*importedAssets*/, string[] /*deletedAssets*/, string[] /*movedAssets*/, string[] /*movedFromAssetPaths*/> PostprocessAllAssets;

        string[] FindAssets(string filter, string[] searchInFolders);

        bool DeleteAssets(string[] paths, List<string> outFailedPaths);
        string AssetPathToGuid(string assetPath);
        string GuidToAssetPath(string guid);
        bool IsValidFolder(string path);
        void Refresh();
        string GetAssetPath(Object obj);
        string GetTextMetaFilePathFromAssetPath(string fileName);
        string[] GetDependencies(string assetPath, bool recursive);
        void SaveAssetIfDirty(Object obj);
        void ImportAsset(string path);
        string[] GetLabels(Object obj);
        void StartAssetEditing();
        void StopAssetEditing();
        Object LoadAssetAtPath(string assetPath);
        Object LoadAssetAtPath(string assetPath, Type type);

        void PingAssetByGuid(string guid);
        bool CanPingAssetByGuid(string guid);
        IEnumerable<string> GetAssetsInFolder(string folder);
    }

    class AssetDatabaseProxy : BaseService<IAssetDatabaseProxy>, IAssetDatabaseProxy
    {
        class AssetPostprocessor : UnityEditor.AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                ServicesContainer.instance.Resolve<AssetDatabaseProxy>().PostprocessAllAssets?.Invoke(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
            }
        }

        public event Action<string[] /*importedAssets*/, string[] /*deletedAssets*/, string[] /*movedAssets*/, string[] /*movedFromAssetPaths*/> PostprocessAllAssets = delegate {};


        // Wrapper AssetDatabase methods
        public string[] FindAssets(string filter, string[] searchInFolders) => AssetDatabase.FindAssets(filter, searchInFolders);

        public bool DeleteAssets(string[] paths, List<string> outFailedPaths) => AssetDatabase.DeleteAssets(paths, outFailedPaths);

        public string AssetPathToGuid(string assetPath) => AssetDatabase.AssetPathToGUID(assetPath);

        public string GuidToAssetPath(string guid) => AssetDatabase.GUIDToAssetPath(guid);

        public bool IsValidFolder(string path) => AssetDatabase.IsValidFolder(path);

        public void Refresh() => AssetDatabase.Refresh();

        public string GetAssetPath(Object obj) => AssetDatabase.GetAssetPath(obj);

        public string GetTextMetaFilePathFromAssetPath(string fileName) => AssetDatabase.GetTextMetaFilePathFromAssetPath(fileName);

        public string[] GetDependencies(string assetPath, bool recursive) => AssetDatabase.GetDependencies(assetPath, recursive);

        public void SaveAssetIfDirty(Object obj) => AssetDatabase.SaveAssetIfDirty(obj);

        public void ImportAsset(string path) => AssetDatabase.ImportAsset(path);

        public string[] GetLabels(Object obj) => AssetDatabase.GetLabels(obj);

        public void StartAssetEditing() => AssetDatabase.StartAssetEditing();

        public void StopAssetEditing() => AssetDatabase.StopAssetEditing();

        public Object LoadAssetAtPath(string assetPath) => AssetDatabase.LoadAssetAtPath<Object>(assetPath);

        public Object LoadAssetAtPath(string assetPath, Type type) => AssetDatabase.LoadAssetAtPath(assetPath, type);

        // End of wrapper AssetDatabase methods


        Object GetAssetObject(string guid)
        {
            return LoadAssetAtPath(GuidToAssetPath(guid));
        }

        public void PingAssetByGuid(string guid)
        {
            var assetObject = GetAssetObject(guid);

            if (assetObject != null)
            {
                EditorGUIUtility.PingObject(assetObject);
            }
        }

        public bool CanPingAssetByGuid(string guid)
        {
            return GetAssetObject(guid) != null;
        }

        public IEnumerable<string> GetAssetsInFolder(string folder)
        {
            var subAssetGuids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
            foreach (var subAssetGuid in subAssetGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(subAssetGuid);
                if (!AssetDatabase.IsValidFolder(path))
                {
                    yield return subAssetGuid;
                }
            }
        }
    }
}
