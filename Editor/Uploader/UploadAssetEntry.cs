using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IUploadAssetEntry
    {
        string Name { get; }
        string Guid { get; }
        Cloud.Assets.AssetType CloudType { get; }
        IReadOnlyCollection<string> Tags { get; }
        IReadOnlyCollection<string> Files { get; }
        IReadOnlyCollection<string> Dependencies { get; }
    }

    [Serializable]
    class AssetUploadEntry : IUploadAssetEntry
    {
        public string Name => m_Name;
        public string Guid => m_Guid;
        public Cloud.Assets.AssetType CloudType => Cloud.Assets.AssetType.Other; // TODO
        public IReadOnlyCollection<string> Tags => m_Tags;
        public IReadOnlyCollection<string> Files => m_Files;
        public IReadOnlyCollection<string> Dependencies => m_Dependencies;

        [SerializeField]
        string m_Name;

        [SerializeField]
        string m_Guid;

        [SerializeField]
        List<string> m_Tags;

        [SerializeField]
        List<string> m_Files;

        [SerializeField]
        List<string> m_Dependencies;

        public AssetUploadEntry(string assetGuid, bool bundleDependencies)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
            m_Name = Path.GetFileNameWithoutExtension(assetPath);
            m_Guid = assetGuid;

            m_Tags = ExtractTags(assetPath).ToList();

            m_Files = new List<string>();
            AddAssetAndItsMetaFile(assetPath);

            if (bundleDependencies)
            {
                foreach (var dependencyPath in AssetDatabase.GetDependencies(assetPath, true))
                {
                    if (m_Files.Contains(dependencyPath))
                        continue;

                    AddAssetAndItsMetaFile(dependencyPath);
                }

                m_Dependencies = new List<string>();
            }
            else
            {
                m_Dependencies = AssetDatabase.GetDependencies(assetPath, false)
                    .Select(AssetDatabase.AssetPathToGUID)
                    .ToList();
            }
        }

        void AddAssetAndItsMetaFile(string assetPath)
        {
            if (!Sanitize(assetPath).StartsWith("assets/"))
                return;

            m_Files.Add(assetPath);
            m_Files.Add(MetafilesHelper.AssetMetaFile(assetPath));
        }

        static IEnumerable<string> ExtractTags(string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

            yield return asset.GetType().Name;

            var extension = Path.GetExtension(assetPath);

            if (extension != null)
            {
                yield return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(extension.TrimStart('.'));
            }

            foreach (var label in AssetDatabase.GetLabels(asset))
            {
                yield return label;
            }

            var isURP = false;
            var isHDRP = false;

            var extractedPackageNames = new HashSet<string>();

            foreach (var dependenciesPath in AssetDatabase.GetDependencies(assetPath, true))
            {
                var sanitisedPath = Sanitize(dependenciesPath);

                if (!isHDRP && sanitisedPath.StartsWith("packages/com.unity.render-pipelines.high-definition/"))
                {
                    isHDRP = true;
                    yield return "HDRP";
                }
                else if (!isURP && sanitisedPath.StartsWith("packages/com.unity.render-pipelines.universal/"))
                {
                    isURP = true;
                    yield return "URP";
                }

                if (ExtractStringBetweenPackages(sanitisedPath, out var packageName))
                {
                    if (!extractedPackageNames.Add(packageName))
                        continue;

                    yield return packageName;
                }
            }
        }

        static string Sanitize(string path)
        {
            return path.Replace('\\', '/').ToLower();
        }

        static bool ExtractStringBetweenPackages(string input, out string packageName)
        {
            packageName = null;
            var match = Regex.Match(input, @"packages/(.*?)/");

            if (!match.Success)
                return false;

            packageName = match.Groups[1].Value.Replace("com.unity.", "");
            return true;
        }
    }
}