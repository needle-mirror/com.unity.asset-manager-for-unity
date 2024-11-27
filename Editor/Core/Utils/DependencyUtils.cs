using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.AssetManager.Core.Editor
{
    static class DependencyUtils
    {
        // Reuse the existing internal method to get all script guids
        static readonly System.Reflection.MethodInfo s_GetAllScriptGuids = Type
            .GetType("UnityEditorInternal.InternalEditorUtility,UnityEditor.dll")
            ?.GetMethod("GetAllScriptGUIDs",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        // Some Unity dependencies like cginc files can only be fetched outside of the AssetDatabase and require the call the internal Unity methods
        static readonly System.Reflection.MethodInfo s_GetSourceAssetImportDependenciesAsGUIDs = Type
            .GetType("UnityEditor.AssetDatabase,UnityEditor.dll")
            ?.GetMethod("GetSourceAssetImportDependenciesAsGUIDs",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        static readonly System.Reflection.MethodInfo s_GetImportedAssetImportDependenciesAsGUIDs = Type
            .GetType("UnityEditor.AssetDatabase,UnityEditor.dll")
            ?.GetMethod("GetImportedAssetImportDependenciesAsGUIDs",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        public static IEnumerable<string> GetValidAssetDependencyGuids(string assetGuid, bool recursive)
        {
            var dependencies = new HashSet<string>();

            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);

            foreach (var path in AssetDatabase.GetDependencies(assetPath, false))
            {
                if (!IsPathInsideAssetsFolder(path))
                    continue;

                var guid = AssetDatabase.AssetPathToGUID(path);
                dependencies.Add(guid);
            }

            try
            {
                foreach (var guid in InvokeMethod(s_GetSourceAssetImportDependenciesAsGUIDs, assetPath))
                {
                    dependencies.Add(guid);
                }

                foreach (var guid in InvokeMethod(s_GetImportedAssetImportDependenciesAsGUIDs, assetPath))
                {
                    dependencies.Add(guid);
                }
            }
            catch (Exception e)
            {
                Utilities.DevLogException(e);
            }

            if (recursive)
            {
                var newDependencies = new HashSet<string>(dependencies);
                foreach (var dependency in newDependencies)
                {
                    dependencies.UnionWith(GetValidAssetDependencyGuids(dependency, true));
                }
            }

            return dependencies;
        }

        public static IEnumerable<string> GetAllScriptGuids()
        {
            return InvokeMethod(s_GetAllScriptGuids);
        }

        static IEnumerable<string> InvokeMethod(System.Reflection.MethodInfo method, string assetPath = null)
        {
            var parameters = assetPath == null ? null : new object[] { assetPath };

            var results = method?.Invoke(null, parameters);

            if (results is not IEnumerable array)
                yield break;

            foreach (var item in array)
            {
                yield return (string)item;
            }
        }

        static bool IsPathInsideAssetsFolder(string assetPath)
        {
            return assetPath.Replace('\\', '/').ToLower().StartsWith("assets/");
        }
    }
}
