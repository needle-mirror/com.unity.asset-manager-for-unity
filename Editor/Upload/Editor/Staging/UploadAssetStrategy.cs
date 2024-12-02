using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.Upload.Editor
{
    static class UploadAssetStrategy
    {
        public static IEnumerable<UploadAssetData> GenerateUploadAssets(IReadOnlyCollection<string> mainGuids,
            IReadOnlyCollection<string> ignoredGuids, UploadSettings settings, Action<string, float> progressCallback = null)
        {
            var dependencies = settings.DependencyMode == UploadDependencyMode.Separate
                ? ResolveDependencies(mainGuids)
                : new HashSet<string>();

            var allGuids = new HashSet<string>(mainGuids);
            allGuids.UnionWith(dependencies);

            // Generate the identifier for each asset first so they can reference to each other
            var identifiers = allGuids.ToDictionary(guid => guid, guid => new AssetIdentifier(guid));

            var cache = new Dictionary<string, UploadAssetData>();

            var total = identifiers.Count;
            var count = 0f;

            foreach (var (guid, identifier) in identifiers)
            {
                progressCallback?.Invoke(guid, count++ / total);
                GenerateUploadAssetRecursive(ignoredGuids, settings, guid, identifiers, identifier, cache);
            }

            // Set the dependencies for each asset
            foreach (var asset in cache.Values)
            {
                asset.IsDependency = !mainGuids.Contains(asset.Guid);
            }

            return cache.Values;
        }

        static UploadAssetData GenerateUploadAssetRecursive(IReadOnlyCollection<string> ignoredGuids, UploadSettings settings, string guid,
            IReadOnlyDictionary<string, AssetIdentifier> identifiers, AssetIdentifier identifier,
            Dictionary<string, UploadAssetData> cache)
        {
            if (cache.TryGetValue(guid, out var result))
            {
                return result;
            }

            // Make sure guid is added to cache to avoid recursive calls
            cache[guid] = null;

            IEnumerable<string> dependencyGuids = null;

            var files = new List<string> { guid };

            switch (settings.DependencyMode)
            {
                case UploadDependencyMode.Embedded:
                    files.AddRange(DependencyUtils.GetValidAssetDependencyGuids(guid, true));
                    break;

                case UploadDependencyMode.Separate:
                    dependencyGuids = DependencyUtils.GetValidAssetDependencyGuids(guid, false).ToList();
                    break;
            }

            var filteredFiles = files.Where(fileGuid => fileGuid == guid || !ignoredGuids.Contains(fileGuid)).ToList();

            var deps = new List<UploadAssetData>();

            if (dependencyGuids != null)
            {
                foreach (var dependencyGuid in dependencyGuids)
                {
                    if (cache.ContainsKey(dependencyGuid))
                        continue;

                    var dep = GenerateUploadAssetRecursive(ignoredGuids, settings, dependencyGuid, identifiers, identifiers[dependencyGuid], cache);

                    if (dep != null)
                    {
                        // A null result means the asset is being processed in a recursive call
                        deps.Add(dep);
                    }
                }
            }

            var assetUploadEntry = new UploadAssetData(identifier, guid, filteredFiles, deps, settings.FilePathMode);

            // NO SONAR
            cache[guid] = assetUploadEntry;

            return assetUploadEntry;
        }

        public static ISet<string> ResolveMainSelection(params string[] guids)
        {
            var processed = new HashSet<string>();

            // Process main assets first
            foreach (var mainGuid in guids)
            {
                processed.UnionWith(ProcessAssetsAndFolders(mainGuid));
            }

            return processed;
        }

        static ISet<string> ResolveDependencies(IReadOnlyCollection<string> mainGuids)
        {
            var processed = new HashSet<string>(mainGuids);

            // Process Dependencies
            foreach (var guid in mainGuids)
            {
                processed.UnionWith(DependencyUtils.GetValidAssetDependencyGuids(guid, true));
            }

            return processed;
        }

        static IEnumerable<string> ProcessAssetsAndFolders(string guid)
        {
            var assetDatabaseProxy = ServicesContainer.instance.Resolve<IAssetDatabaseProxy>();
            var assetPath = assetDatabaseProxy.GuidToAssetPath(guid);
            return assetDatabaseProxy.IsValidFolder(assetPath)
                ? assetDatabaseProxy.GetAssetsInFolder(assetPath)
                : new[] { guid };
        }
    }
}
