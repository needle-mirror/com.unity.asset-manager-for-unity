using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
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

        public DependencyAssetResult(IAssetData asset)
        {
            Identifier = asset.Identifier;
            AssetData = asset;
        }
    }

    static class AssetDataDependencyHelper
    {
        const char k_AssetIdAssetVersionSeparator = '_';

        const string k_DependencyExtension = ".am4u_dep";
        const string k_GuidExtension = ".am4u_guid";

        [Obsolete("Only used for backwards compatibility with system file references.")]
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

        public static async IAsyncEnumerable<AssetIdentifier> LoadDependenciesAsync(IAssetData assetData,
            [EnumeratorCancellation] CancellationToken token)
        {
            var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
            var assetsProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();

            // Flag for backwards compatibility - if the asset has no dependencies, check for system file dependencies
            var hasDependencies = false;

            await foreach (var dependency in assetsProvider.GetDependenciesAsync(assetData.Identifier, Range.All, token))
            {
                hasDependencies = true;
                yield return dependency;
            }

            if (!hasDependencies)
            {
#pragma warning disable 618 // Maintain for backwards compatibility with old assets that have dependencies stored in the asset itself
                var systemFileDependencies =
                    LoadSystemFileDependenciesAsync(assetDataManager, assetsProvider, assetData, token);
                await foreach (var dependency in systemFileDependencies)
                {
                    yield return dependency;
                }
#pragma warning restore 618
            }
        }

        [Obsolete("Only used for backwards compatibility with system file references.")]
        static async IAsyncEnumerable<AssetIdentifier> LoadSystemFileDependenciesAsync(
            IAssetDataManager assetDataManager, IAssetsProvider assetsProvider, IAssetData assetData,
            [EnumeratorCancellation] CancellationToken token)
        {
            var files = await GetFilesAsync(assetData, token);

            var dependencies = files.Where(f => IsADependencySystemFile(f.Path)).ToList();

            if (!dependencies.Any())
            {
                yield break;
            }

            foreach (var dependency in dependencies)
            {
                DecodeDependencySystemFilename(dependency.Path, out var assetId, out var assetVersion);

                var assetIdentifier = assetData.Identifier
                    .WithAssetId(assetId)
                    .WithVersion(assetVersion);

                // This should never happen, but if the version fails to parse, try to fetch the latest version from the provider
                if (string.IsNullOrEmpty(assetVersion))
                {
                    assetVersion = await GetLatestVersionAsync(assetsProvider, assetIdentifier, token);
                    assetIdentifier = assetIdentifier.WithVersion(assetVersion);
                }

                // IN CASE no version is found, skip the dependency
                if (string.IsNullOrEmpty(assetVersion))
                {
                    Debug.LogError("No version found for dependency with asset id: " + assetId);
                    continue;
                }

                yield return assetIdentifier;
            }
        }

        public static async Task<AssetData> GetAssetAssociatedWithGuidAsync(string assetGuid, string organizationId,
            string projectId, CancellationToken token)
        {
            Utilities.DevAssert(!string.IsNullOrEmpty(assetGuid));
            Utilities.DevAssert(!string.IsNullOrEmpty(organizationId));
            Utilities.DevAssert(!string.IsNullOrEmpty(projectId));

            AssetData existingAsset = null;

            // First, check if the guid is associated with an asset that was already imported
            var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();
            var importedAssetInfos = assetDataManager.GetImportedAssetInfosFromFileGuid(assetGuid);

            if (importedAssetInfos == null)
                return null;

            // Then check if the file is an embedded dependency in all associated assets
            AssetData assetData = null;
            foreach (var importedAssetInfo in importedAssetInfos)
            {
                if (importedAssetInfo.FileInfos.Exists(
                        x => Utilities.GetValidAssetDependencyGuids(x.Guid, true).Contains(assetGuid)))
                {
                    continue;
                }

                var importedAssetData = importedAssetInfo.AssetData;

                // Imported asset must match current project and still exist in the provider for it to be recycled
                if (importedAssetData == null || importedAssetData.Identifier.OrganizationId != organizationId ||
                    importedAssetData.Identifier.ProjectId != projectId)
                {
                    continue;
                }

                assetData = importedAssetData as AssetData;
                break;
            }

            if (assetData != null)
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

        static async Task<string> GetLatestVersionAsync(IAssetsProvider assetsProvider, AssetIdentifier assetIdentifier, CancellationToken token)
        {
            try
            {
                var asset = await assetsProvider.GetLatestAssetVersionAsync(assetIdentifier, token);
                return asset?.Identifier.Version;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            return null;
        }

        static async Task<List<IAssetDataFile>> GetFilesAsync(IAssetData assetData, CancellationToken token)
        {
            var files = assetData.SourceFiles.Union(assetData.UVCSFiles).ToList();
            if (files.Count == 0 && assetData is AssetData ad)
            {
                await ad.RefreshSourceFilesAndPrimaryExtensionAsync(token);
                files = assetData.SourceFiles.ToList();
            }

            return files;
        }
    }
}
