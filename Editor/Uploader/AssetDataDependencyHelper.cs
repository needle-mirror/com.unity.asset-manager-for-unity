using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class DependencyAsset // TODO Put behind an interface
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
        const int k_MaxRunningTasks = 5;
        const char k_AssetIdAssetVersionSeparator = '_';

        public static readonly string DependencyExtension = ".am4u_dep";
        public static readonly string GuidExtension = ".am4u_guid";

        static readonly SemaphoreSlim s_SearchForAssetWithGuidSemaphore = new(k_MaxRunningTasks);

        public static string EncodeDependencySystemFilename(string assetId, string assetVersion)
        {
            return $"{assetId}{k_AssetIdAssetVersionSeparator}{assetVersion}{DependencyExtension}";
        }

        public static bool DecodeDependencySystemFilename(string filename, out string assetId, out string assetVersion)
        {
            assetId = string.Empty;
            assetVersion = string.Empty;

            if (Path.GetExtension(filename) != DependencyExtension)
                return false;

            var filenameParts = Path.GetFileNameWithoutExtension(filename)
                .Split(k_AssetIdAssetVersionSeparator, StringSplitOptions.RemoveEmptyEntries);
            
            assetId = filenameParts[0];
            assetVersion = filenameParts.Length >= 2 ? filenameParts[1] : string.Empty;
            return true;
        }

        public static string GenerateGuidSystemFilename(IUploadAssetEntry uploadAssetEntry)
        {
            return $"{uploadAssetEntry.Guid}{GuidExtension}";
        }

        public static bool IsAGuidSystemFile(string filename)
        {
            return filename.ToLower().EndsWith(GuidExtension);
        }

        public static bool IsADependencySystemFile(string filename)
        {
            return filename.ToLower().EndsWith(DependencyExtension);
        }

        public static bool IsASystemFile(string filename)
        {
            return IsAGuidSystemFile(filename) || IsADependencySystemFile(filename);
        }

        public static async IAsyncEnumerable<DependencyAssetResult> LoadDependenciesAsync(IAssetData assetData, bool recursive, [EnumeratorCancellation] CancellationToken token)
        {
            // Update AssetData with the latest cloud data
            var files = assetData.sourceFiles.ToList();
            if (files.Count == 0)
            {
                await ((AssetData)assetData).RefreshSourceFilesAndPrimaryExtensionAsync(token);
                files = assetData.sourceFiles.ToList();
            }

            var dependencies = files.Where(f => IsADependencySystemFile(f.path)).ToList();

            if (!dependencies.Any())
                yield break;

            var parentAssetDescriptor = assetData.identifier.ToAssetDescriptor();

            var assetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();

            foreach (var dependency in dependencies)
            {
                DecodeDependencySystemFilename(dependency.path, out var assetId, out var assetVersion);
                if (string.IsNullOrEmpty(assetVersion))
                    assetVersion = await Services.AssetVersionsSearch.GetFirstVersionAsync(new ProjectId(assetData.identifier.projectId), new AssetId(assetId), token);
                
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
        
        public static async Task<IAsset> SearchForAssetWithGuid(string organizationId, string projectId, string guid, CancellationToken token)
        {
            if (string.IsNullOrEmpty(organizationId) || string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(guid))
                return null;

            await s_SearchForAssetWithGuidSemaphore.WaitAsync(token);

            var assetSearchFilter = new AssetSearchFilter();
            assetSearchFilter.Include().Files.Path.WithValue($"{guid}*");

            var assetsProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
            var assets = new List<IAsset>();
            await foreach (var asset in assetsProvider.SearchAsync(organizationId, new[] { projectId }, assetSearchFilter, 0, Constants.DefaultPageSize, token))
            {
                assets.Add(asset);
            }

            s_SearchForAssetWithGuidSemaphore.Release();

            return assets.FirstOrDefault();
        }
    }
}