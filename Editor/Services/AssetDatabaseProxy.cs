using System;
using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Unity.AssetManager.Editor
{
    interface IAssetDatabaseProxy : IService
    {
        event Action<string[] /*importedAssets*/, string[] /*deletedAssets*/, string[] /*movedAssets*/, string[] /*movedFromAssetPaths*/> PostprocessAllAssets;

        string[] FindAssets(string filter, string[] searchInFolders);

        bool DeleteAssets(string[] paths, List<string> outFailedPaths);
        string[] GetSubFolders(string folderPath);
        string AssetPathToGuid(string assetPath);
        string GuidToAssetPath(string guid);
        void Refresh();
        Object LoadAssetAtPath(string assetPath);
        void StartAssetEditing();
        void StopAssetEditing();
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

        public string[] FindAssets(string filter, string[] searchInFolders) => AssetDatabase.FindAssets(filter, searchInFolders);

        public bool DeleteAssets(string[] paths, List<string> outFailedPaths) => AssetDatabase.DeleteAssets(paths, outFailedPaths);

        public string[] GetSubFolders(string folderPath) => AssetDatabase.GetSubFolders(folderPath);

        public string AssetPathToGuid(string assetPath) => AssetDatabase.AssetPathToGUID(assetPath);

        public string GuidToAssetPath(string guid) => AssetDatabase.GUIDToAssetPath(guid);

        public void Refresh() => AssetDatabase.Refresh();
        public Object LoadAssetAtPath(string assetPath) => AssetDatabase.LoadAssetAtPath<Object>(assetPath);

        public void StartAssetEditing() => AssetDatabase.StartAssetEditing();

        public void StopAssetEditing() => AssetDatabase.StopAssetEditing();

        public static IEnumerable<string> GetAssetDependencies(IEnumerable<string> assetGuids)
        {
            var allDependencies = new HashSet<string>();

            foreach (var assetGuid in assetGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                foreach (var dependencyPath in AssetDatabase.GetDependencies(assetPath, true))
                {
                    allDependencies.Add(dependencyPath);
                }
            }

            foreach (var dependency in allDependencies)
            {
                yield return AssetDatabase.AssetPathToGUID(dependency);
            }
        }

        public static IEnumerable<string> GetAssetsInFolder(string folder)
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
