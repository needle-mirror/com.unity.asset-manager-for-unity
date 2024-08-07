using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Unity.AssetManager.Editor
{
    static class TagExtractor
    {
        public static IEnumerable<string> ExtractFromAsset(string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

            if (asset == null)
            {
                Utilities.DevLogError($"Cannot load asset {assetPath} to extract all tags.");
            }

            if (AssetDatabase.IsValidFolder(assetPath))
            {
                yield return "Folder";
            }
            else
            {
                if (asset != null)
                {
                    yield return asset.GetType().Name;
                }

                var extension = Path.GetExtension(assetPath);

                if (!string.IsNullOrWhiteSpace(extension))
                {
                    yield return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(extension.TrimStart('.'));
                }
            }

            if (asset != null)
            {
                foreach (var label in AssetDatabase.GetLabels(asset))
                {
                    yield return label;
                }
            }

            foreach (var packageTag in ExtractPackageTags(assetPath))
            {
                yield return packageTag;
            }
        }

        static IEnumerable<string> ExtractPackageTags(string assetPath)
        {
            var processedPackages = new HashSet<string>();

            foreach (var dependenciesPath in AssetDatabase.GetDependencies(assetPath, true))
            {
                if (!dependenciesPath.StartsWith("packages", StringComparison.CurrentCultureIgnoreCase))
                    continue;

                if (!processedPackages.Add(dependenciesPath))
                    continue;

                if (ExtractStringBetweenPackages(dependenciesPath, out var packageName))
                {
                    if (packageName.Equals("render-pipelines.high-definition", StringComparison.InvariantCultureIgnoreCase))
                        yield return "HDRP";

                    if (packageName.Equals("render-pipelines.universal", StringComparison.InvariantCultureIgnoreCase))
                        yield return "URP";

                    yield return packageName;
                }
            }
        }

        static bool ExtractStringBetweenPackages(string input, out string packageName)
        {
            input = input.Replace('\\', '/').ToLower();

            packageName = null;
            var match = Regex.Match(input, @"packages/(.*?)/");

            if (!match.Success)
            {
                return false;
            }

            packageName = match.Groups[1].Value.Replace("com.unity.", "");
            return true;
        }
    }
}