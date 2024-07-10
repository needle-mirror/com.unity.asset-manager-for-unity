using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Common;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class DependencyAsset // Might need to add an interface
    {
        [SerializeField]
        AssetIdentifier m_Identifier;

        [SerializeReference]
        IAssetData m_AssetData;

        public AssetIdentifier Identifier => m_Identifier;
        public IAssetData AssetData => m_AssetData;

        public DependencyAsset(AssetIdentifier identifier, IAssetData assetData)
        {
            m_Identifier = identifier;
            m_AssetData = assetData;
        }
    }

    struct DependencyAssetResult
    {
        public AssetIdentifier Identifier { get; }
        public IAssetData AssetData { get; }

        public DependencyAssetResult(AssetIdentifier identifier, IAssetData assetData)
        {
            Identifier = identifier;
            AssetData = assetData;
        }
    }

    static class AssetDataDependencyHelper
    {
        const char k_AssetIdAssetVersionSeparator = '_';

        static readonly string k_DependencyExtension = ".am4u_dep";
        static readonly string k_GuidExtension = ".am4u_guid";

        public static string EncodeDependencySystemFilename(string assetId, string assetVersion)
        {
            return $"{assetId}{k_AssetIdAssetVersionSeparator}{assetVersion}{k_DependencyExtension}";
        }

        static void DecodeDependencySystemFilename(string filename, out string assetId, out string assetVersion)
        {
            assetId = string.Empty;
            assetVersion = string.Empty;

            if (Path.GetExtension(filename) != k_DependencyExtension)
                return;

            var filenameParts = Path.GetFileNameWithoutExtension(filename)
                .Split(k_AssetIdAssetVersionSeparator, StringSplitOptions.RemoveEmptyEntries);

            assetId = filenameParts[0];
            assetVersion = filenameParts.Length >= 2 ? filenameParts[1] : string.Empty;
        }

        static bool IsAGuidSystemFile(string filename)
        {
            // am4u_guid files are deprecated, but we still need this code to hide them in previously generated cloud assets.
            return filename.ToLower().EndsWith(k_GuidExtension);
        }

        static bool IsADependencySystemFile(string filename)
        {
            return filename.ToLower().EndsWith(k_DependencyExtension);
        }

        public static bool IsASystemFile(string filename)
        {
            return IsAGuidSystemFile(filename) || IsADependencySystemFile(filename);
        }

        public static async IAsyncEnumerable<DependencyAssetResult> LoadDependenciesAsync(IAssetData assetData,
            bool recursive, [EnumeratorCancellation] CancellationToken token)
        {
            // Update AssetData with the latest cloud data
            var files = assetData.SourceFiles.ToList();
            if (files.Count == 0)
            {
                await ((AssetData)assetData).RefreshSourceFilesAndPrimaryExtensionAsync(token);
                files = assetData.SourceFiles.ToList();
            }

            var dependencies = files.Where(f => IsADependencySystemFile(f.Path)).ToList();

            if (!dependencies.Any())
            {
                yield break;
            }

            var parentAssetDescriptor = assetData.Identifier.ToAssetDescriptor();

            var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();

            foreach (var dependency in dependencies)
            {
                DecodeDependencySystemFilename(dependency.Path, out var assetId, out var assetVersion);

                // This should never happen, but if the version fails to parse, try to fetch the latest version from the cloud
                if (string.IsNullOrEmpty(assetVersion))
                {
                    assetVersion = await GetLatestVersionAsync(parentAssetDescriptor.ProjectDescriptor, new AssetId(assetId), token);
                }

                // IN CASE no version is found, skip the dependency
                if (string.IsNullOrEmpty(assetVersion))
                {
                    Debug.LogError("No version found for dependency with asset id: " + assetId);
                    continue;
                }

                var assetDescriptor = new AssetDescriptor(parentAssetDescriptor.ProjectDescriptor,
                    new AssetId(assetId),
                    new AssetVersion(assetVersion));
                var assetIdentifier = new AssetIdentifier(assetDescriptor);

                var dependencyAssetData = await assetDataManager.GetOrSearchAssetData(assetIdentifier, token);

                if (dependencyAssetData != null)
                {
                    var result = new DependencyAssetResult(assetIdentifier, dependencyAssetData);
                    yield return result;
                }

                if (recursive)
                {
                    await foreach (var childDependency in LoadDependenciesAsync(dependencyAssetData, true, token))
                    {
                        yield return childDependency;
                    }
                }
            }
        }

        public static async Task<AssetData> GetAssetAssociatedWithGuidAsync(string assetGuid, string organizationId, string projectId, CancellationToken token)
        {
            Utilities.DevAssert(!string.IsNullOrEmpty(assetGuid));
            Utilities.DevAssert(!string.IsNullOrEmpty(organizationId));
            Utilities.DevAssert(!string.IsNullOrEmpty(projectId));

            AssetData existingAsset = null;

            // First, check if the guid is associated with an asset that was already imported
            var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
            var assetData = assetDataManager.GetImportedAssetInfosFromFileGuid(assetGuid)?.FirstOrDefault()?.AssetData as AssetData;

            // Imported asset must match current project and still exists on the cloud for it to be recycled
            if (assetData != null && assetData.Identifier.OrganizationId == organizationId && assetData.Identifier.ProjectId == projectId)
            {
                var assetsProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
                var status = await assetsProvider.CompareAssetWithCloudAsync(assetData, token);

                if (status != AssetComparisonResult.NotFoundOrInaccessible)
                {
                    existingAsset = assetData;
                }
            }

            return existingAsset;
        }

        static async Task<string> GetLatestVersionAsync(ProjectDescriptor projectDescriptor, AssetId assetId, CancellationToken token)
        {
            try
            {
                var assetProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
                var asset = await assetProvider.GetLatestAssetVersionAsync(projectDescriptor, assetId, token);
                return asset?.Identifier.Version;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            return null;
        }
    }
}
