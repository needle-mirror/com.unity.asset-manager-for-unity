using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.AssetsEmbedded;
using Unity.Cloud.CommonEmbedded;
using Unity.Cloud.CommonEmbedded.Runtime;
using Unity.Cloud.IdentityEmbedded;
using Unity.Cloud.IdentityEmbedded.Editor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using Task = System.Threading.Tasks.Task;

namespace Unity.AssetManager.Core.Editor
{
    enum AssetComparisonResult
    {
        None,
        UpToDate,
        OutDated,
        NotFoundOrInaccessible,
        Unknown
    }

    enum AuthenticationState
    {
        /// <summary>
        /// Indicates the application is waiting for the completion of the initialization.
        /// </summary>
        AwaitingInitialization,
        /// <summary>
        /// Indicates when an authenticated user is logged in.
        /// </summary>
        LoggedIn,
        /// <summary>
        /// Indicates no authenticated user is available.
        /// </summary>
        LoggedOut,
        /// <summary>
        /// Indicates the application is waiting for the completion of a login operation.
        /// </summary>
        AwaitingLogin,
        /// <summary>
        /// Indicates the application is waiting for the completion of a logout operation.
        /// </summary>
        AwaitingLogout
    };

    [Serializable]
    class AssetSearchFilter
    {
        public List<string> Searches;
        public string CreatedBy;
        public string UpdatedBy;
        public string Status;
        public UnityAssetType? UnityType;
        public string Collection;
    }

    enum AssetSearchGroupBy
    {
        Name,
        Status,
        CreatedBy,
        UpdatedBy,
    }

    enum SortField
    {
        Name,
        Updated,
        Created,
        Description,
        PrimaryType,
        Status,
        ImportStatus
    }

    enum SortingOrder
    {
        Ascending,
        Descending
    }

    interface IAssetsProvider : IService
    {
        // Authentication

        event Action<AuthenticationState> AuthenticationStateChanged;
        AuthenticationState AuthenticationState { get; }

        // Organizations

        Task<OrganizationInfo> GetOrganizationInfoAsync(string organizationId, CancellationToken token);

        IAsyncEnumerable<IOrganization> ListOrganizationsAsync(Range range, CancellationToken token);

        Task<IOrganization> GetOrganizationAsync(string organizationId);

        Task<StorageUsage> GetOrganizationCloudStorageUsageAsync(string organizationId, CancellationToken token = default);

        IAsyncEnumerable<IMemberInfo> GetOrganizationMembersAsync(string organizationId, Range range,
            CancellationToken token);

        // Projects

        Task<Dictionary<string, string>> GetProjectIconUrlsAsync(string organizationId, CancellationToken token);

        Task EnableProjectAsync(CancellationToken token = default);

        Task<ProjectInfo> GetProjectInfoAsync(string organizationId, string projectId, CancellationToken token);

        // Collections

        Task CreateCollectionAsync(CollectionInfo collectionInfo, CancellationToken token);
        Task DeleteCollectionAsync(CollectionInfo collectionInfo, CancellationToken token);
        Task RenameCollectionAsync(CollectionInfo collectionInfo, string newName, CancellationToken token);

        // Assets

        Task<AssetData> GetAssetAsync(AssetIdentifier assetIdentifier, CancellationToken token);
        Task<AssetData> GetLatestAssetVersionAsync(AssetIdentifier assetIdentifier, CancellationToken token);
        IAsyncEnumerable<AssetData> ListVersionInDescendingOrderAsync(AssetIdentifier assetIdentifier, CancellationToken token);
        Task<AssetData> FindAssetAsync(string organizationId, string assetId, string assetVersion, CancellationToken token);

        IAsyncEnumerable<AssetData> SearchAsync(string organizationId, IEnumerable<string> projectIds,
             AssetSearchFilter assetSearchFilter, SortField sortField, SortingOrder sortingOrder, int startIndex, int pageSize, CancellationToken token);
        Task<List<string>> GetFilterSelectionsAsync(string organizationId, IEnumerable<string> projectIds,
            AssetSearchFilter assetSearchFilter,
            AssetSearchGroupBy groupBy, CancellationToken token);

        Task<AssetData> CreateAssetAsync(ProjectIdentifier projectIdentifier, AssetCreation assetCreation, CancellationToken token);
        Task<AssetData> CreateUnfrozenVersionAsync(AssetData assetData, CancellationToken token);

        Task RemoveAsset(AssetIdentifier assetIdentifier, CancellationToken token);

        Task UpdateAsync(AssetData assetData, AssetUpdate assetUpdate, CancellationToken token);
        Task UpdateStatusAsync(AssetData assetData, string statusName, CancellationToken token);
        Task FreezeAsync(AssetData assetData, string changeLog, CancellationToken token);

        Task<Uri> GetPreviewUrlAsync(AssetData assetData, int maxDimension, CancellationToken token);

        Task<bool> AssetExistsOnCloudAsync(BaseAssetData assetData, CancellationToken token);
        Task<AssetComparisonResult> CompareAssetWithCloudAsync(BaseAssetData assetData, CancellationToken token);

        IAsyncEnumerable<AssetIdentifier> GetDependenciesAsync(AssetIdentifier assetIdentifier, Range range, CancellationToken token);
        IAsyncEnumerable<AssetIdentifier> GetDependentsAsync(AssetIdentifier assetIdentifier, Range range, CancellationToken token);
        Task UpdateDependenciesAsync(AssetIdentifier assetIdentifier, IEnumerable<AssetIdentifier> assetDependencies, CancellationToken token);

        // Files

        Task<IReadOnlyDictionary<string, Uri>> GetAssetDownloadUrlsAsync(AssetData assetData, IProgress<FetchDownloadUrlsProgress> progress, CancellationToken token);

        Task<AssetDataFile> UploadThumbnail(AssetData assetData, Texture2D thumbnail, IProgress<HttpProgress> progress, CancellationToken token);
        Task RemoveThumbnail(AssetData assetData, CancellationToken token);

        Task<AssetDataFile> UploadFile(AssetData assetData, string destinationPath, Stream stream, IProgress<HttpProgress> progress, CancellationToken token);
        Task RemoveAllFiles(AssetData assetData, CancellationToken token);

        IAsyncEnumerable<AssetDataFile> ListFilesAsync(AssetData assetData, Range range, CancellationToken token);
    }

    static class AssetsProviderUploadExtensions
    {
        public static async Task UploadFile(this IAssetsProvider assetsProvider, AssetData assetData, string destinationPath, string sourcePath, IProgress<HttpProgress> progress, CancellationToken token)
        {
            await using var stream = File.OpenRead(sourcePath);
            await assetsProvider.UploadFile(assetData, destinationPath, stream, progress, token);
        }
    }

    [Serializable]
    class AssetsSdkProvider : BaseService<IAssetsProvider>, IAssetsProvider
    {
        const string k_ThumbnailFilename = "unity_thumbnail.png";
        const string k_UVCSUrl = "cloud.plasticscm.com";
        const int k_MaxNumberOfFilesForAssetDownloadUrlFetch = 100;
        static readonly int k_MaxConcurrentFileDeleteTasks = 20;
        static readonly string k_DefaultCollectionDescription = "none";

        [SerializeReference]
        IUnityConnectProxy m_UnityConnectProxy;

        [SerializeReference]
        ISettingsManager m_SettingsManager;

        IAssetRepository m_AssetRepositoryOverride;

        IAssetRepository AssetRepository => m_AssetRepositoryOverride ?? Services.AssetRepository;

        public AssetsSdkProvider() { }

        /// <summary>
        /// Internal constructor that allow the IAssetRepository to be overriden. Only used for testing.
        ///
        /// IMPORTANT: Since m_AssetRepositoryOverride does not support domain reload, the AssetsSdkProvider constructed cannot
        /// be used across domain reloads
        /// </summary>
        /// <param name="assetRepository"></param>
        internal AssetsSdkProvider(IAssetRepository assetRepository)
        {
            m_AssetRepositoryOverride = assetRepository;
        }

        [ServiceInjection]
        public void Inject(IUnityConnectProxy unityConnectProxy, ISettingsManager settingsManager)
        {
            m_UnityConnectProxy = unityConnectProxy;
            m_SettingsManager = settingsManager;
        }

        public event Action<AuthenticationState> AuthenticationStateChanged;

        public AuthenticationState AuthenticationState => Map(Services.AuthenticationState);

        public override void OnEnable()
        {
            // AMECO-3518
            ResetServiceAuthorizerAwaitingExchangeOperationState();

            Services.AuthenticationStateChanged += OnAuthenticationStateChanged;
            Services.InitAuthenticatedServices();
        }

        // AMECO-3518 Hack to avoid infinite "Awaiting Unity Hub User Session" loop that can happen after two consecutive domain reloads
        // Delete this once the fix is inside Identity.
        void ResetServiceAuthorizerAwaitingExchangeOperationState()
        {
            var unityEditorServiceAuthorizerType = typeof(UnityEditorServiceAuthorizer);
            var field = unityEditorServiceAuthorizerType.GetField("m_AwaitingExchangeOperation", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(UnityEditorServiceAuthorizer.instance, false);
        }

        public override void OnDisable()
        {
            Services.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        }

        void OnAuthenticationStateChanged()
        {
            AuthenticationStateChanged?.Invoke(Map(Services.AuthenticationState));
        }

        public async Task<OrganizationInfo> GetOrganizationInfoAsync(string organizationId, CancellationToken token)
        {
            var t = new Stopwatch();
            t.Start();

#if UNITY_2021
            var orgFromOrgName = await GetOrganizationFromOrganizationName(organizationId);
            organizationId = orgFromOrgName?.Id.ToString();
#endif

            var organizationInfo = new OrganizationInfo {Id = organizationId, Name = "none"};
            if (organizationId == "none")
            {
                return organizationInfo;
            }

            var organization = await GetOrganizationAsync(organizationInfo.Id);
            if (organization == null)
            {
                return organizationInfo;
            }

            organizationInfo.Name = organization.Name;

            await foreach (var project in GetCurrentUserProjectList(organizationId, Range.All, token))
            {
                if (project == null)
                    continue;

                var projectInfo = new ProjectInfo
                {
                    Id = project.Descriptor.ProjectId.ToString(),
                    Name = project.Name
                };

                organizationInfo.ProjectInfos.Add(projectInfo);

                _ = GetCollections(project,
                    collections =>
                    {
                        projectInfo.SetCollections(collections.Select(c => new CollectionInfo
                        {
                            OrganizationId = organizationId,
                            ProjectId = projectInfo.Id,
                            Name = c.Name,
                            ParentPath = c.ParentPath
                        }));
                    },
                    token);
            }

            t.Stop();

            Utilities.DevLog($"Fetching {organizationInfo.ProjectInfos.Count} Projects took {t.ElapsedMilliseconds}ms");

            var definitionsQuery = AssetRepository.QueryFieldDefinitions(new OrganizationId(organizationId)).ExecuteAsync(token);
            await foreach(var result in definitionsQuery)
            {
                MetadataFieldType fieldType = result.Type switch
                {
                    FieldDefinitionType.Boolean => MetadataFieldType.Boolean,
                    FieldDefinitionType.Text => MetadataFieldType.Text,
                    FieldDefinitionType.Number => MetadataFieldType.Number,
                    FieldDefinitionType.Url => MetadataFieldType.Url,
                    FieldDefinitionType.Timestamp => MetadataFieldType.Timestamp,
                    FieldDefinitionType.User => MetadataFieldType.User,
                    FieldDefinitionType.Selection => MetadataFieldType.Selection,
                    _ => MetadataFieldType.Selection
                };

                organizationInfo.MetadataFieldDefinitions.Add(new MetadataFieldDefinition(result.Descriptor.FieldKey,
                    result.DisplayName, fieldType));
            }

            return organizationInfo;
        }

        public async Task<ProjectInfo> GetProjectInfoAsync(string organizationId, string projectId, CancellationToken token)
        {
           var assetProject= await AssetRepository.GetAssetProjectAsync(new ProjectDescriptor(new OrganizationId(organizationId), new ProjectId(projectId)), token);

            var projectInfo = new ProjectInfo
            {
                Id = projectId,
                Name = assetProject.Name
            };

            await GetCollections(assetProject,
                collections =>
                {
                    projectInfo.SetCollections(collections.Select(c => new CollectionInfo
                    {
                        OrganizationId = organizationId,
                        ProjectId = projectId,
                        Name = c.Name,
                        ParentPath = c.ParentPath
                    }));
                },
                token);

            return projectInfo;
        }

        Task GetCollections(IAssetProject project, Action<IEnumerable<IAssetCollection>> successCallback, CancellationToken token)
        {
            var asyncLoad = new AsyncLoadOperation();
            return asyncLoad.Start(t => project.ListCollectionsAsync(Range.All, CancellationTokenSource.CreateLinkedTokenSource(t, token).Token),
                null,
                successCallback,
                null,
                () => Utilities.DevLog($"Cancelled fetching collections for {project.Name}."),
                ex => Utilities.DevLog($"Error fetching collections for {project.Name}: {ex.Message}"));
        }

        public IAsyncEnumerable<IOrganization> ListOrganizationsAsync(Range range, CancellationToken token)
        {
            return Services.OrganizationRepository.ListOrganizationsAsync(range, token);
        }

        public async IAsyncEnumerable<AssetData> SearchAsync(string organizationId, IEnumerable<string> projectIds,
            AssetSearchFilter assetSearchFilter, SortField sortField, SortingOrder sortingOrder, int startIndex, int pageSize,
            [EnumeratorCancellation] CancellationToken token)
        {
            var cloudAssetSearchFilter = Map(assetSearchFilter);
            var cloudSortingOrder = Map(sortingOrder);

            var range = new Range(startIndex, startIndex + pageSize);

            Utilities.DevLog($"Fetching {range} Assets ...");

            var t = new Stopwatch();
            t.Start();

            var count = 0;
            await foreach (var asset in SearchAsync(organizationId, projectIds, cloudAssetSearchFilter, sortField.ToString(), cloudSortingOrder, range, token))
            {
                yield return asset;
                ++count;
            }

            t.Stop();

            Utilities.DevLog($"Fetched {count} Assets from {range} in {t.ElapsedMilliseconds}ms");
        }

        public async Task<StorageUsage> GetOrganizationCloudStorageUsageAsync(string organizationId, CancellationToken token = default)
        {
            var organization = await GetOrganizationAsync(organizationId);
            var cloudStorageUsage = await organization.GetCloudStorageUsageAsync(token);
            return Map(cloudStorageUsage);
        }

#if UNITY_2021
        async Task<IOrganization> GetOrganizationFromOrganizationName(string organizationName)
        {
            // First call to backend requires a valid authentication state
            while (!Services.AuthenticationState.Equals(AuthenticationState.LoggedIn))
            {
                await Task.Delay(200);
            }

            var organizationsAsync = Services.OrganizationRepository.ListOrganizationsAsync(Range.All);

            await foreach (var organization in organizationsAsync)
            {
                if (CreateTagFromOrganizationName(organization.Name) == organizationName)
                {
                    return organization;
                }
            }

            return null;
        }

        // organization names that are coming out of CloudProjectSettings.organizationId are formatted as tag
        string CreateTagFromOrganizationName(string organizationName)
        {
            return organizationName.ToLowerInvariant().Replace(" ", "-");
        }
#endif

        public async Task<Dictionary<string, string>> GetProjectIconUrlsAsync(string organizationId, CancellationToken token)
        {
            var selectedOrg = await GetOrganizationAsync(organizationId);
            if (selectedOrg == null)
            {
                return null;
            }

            var projectIconUrls = new Dictionary<string, string>();
            await foreach (var project in selectedOrg.ListProjectsAsync(Range.All, token))
            {
                if (project == null)
                    continue;

                var iconUrl = project.IconUrl;
                if (!string.IsNullOrEmpty(iconUrl))
                {
                    projectIconUrls[project.Descriptor.ProjectId.ToString()] = iconUrl;
                }
            }

            return projectIconUrls;
        }

        public async Task<List<string>> GetFilterSelectionsAsync(string organizationId, IEnumerable<string> projectIds,
            AssetSearchFilter assetSearchFilter, AssetSearchGroupBy groupBy, CancellationToken token)
        {
            var cloudAssetSearchFilter = Map(assetSearchFilter);
            var cloudGroupBy = Map(groupBy);

            var strongTypedOrgId = new OrganizationId(organizationId);
            var projectDescriptors = projectIds.Select(p => new ProjectDescriptor(strongTypedOrgId, new ProjectId(p))).ToList();

            var groupAndCountAssetsQueryBuilder = AssetRepository.GroupAndCountAssets(projectDescriptors)
                .SelectWhereMatchesFilter(cloudAssetSearchFilter);
            var aggregation = await groupAndCountAssetsQueryBuilder.ExecuteAsync(cloudGroupBy, token);

            var result = aggregation.Keys.Distinct().ToList();
            result.Sort();

            return result;
        }

        public async Task<AssetData> CreateAssetAsync(ProjectIdentifier projectIdentifier, AssetCreation assetCreation, CancellationToken token)
        {
            var projectDescriptor = Map(projectIdentifier);
            var cloudAssetCreation = Map(assetCreation);
            var project = await AssetRepository.GetAssetProjectAsync(projectDescriptor, token);
            var asset = await project.CreateAssetAsync(cloudAssetCreation, token);

            return Map(asset);
        }

        public async Task<AssetData> CreateUnfrozenVersionAsync(AssetData assetData, CancellationToken token)
        {
            var asset = await InternalGetAssetAsync(assetData, token);
            if (asset != null)
            {
                var unfrozenVersion = await asset.CreateUnfrozenVersionAsync(token);
                return await Map(unfrozenVersion, token);
            }

            return null;
        }

        Task<IAsset> InternalGetAssetAsync(AssetData assetData, CancellationToken token)
        {
            return assetData == null
                ? Task.FromResult<IAsset>(null)
                : InternalGetAssetAsync(assetData.Identifier, token);
        }

        async Task<IAsset> InternalGetAssetAsync(AssetIdentifier assetIdentifier, CancellationToken token)
        {
            var asset = await AssetRepository.GetAssetAsync(Map(assetIdentifier), token);
            return asset;
        }

        public async Task RemoveAsset(AssetIdentifier assetIdentifier, CancellationToken token)
        {
            if (assetIdentifier == null)
            {
                return;
            }

            var project = await AssetRepository.GetAssetProjectAsync(Map(assetIdentifier.ProjectIdentifier), token);
            if (project != null)
            {
                await project.UnlinkAssetsAsync(new[] { new AssetId(assetIdentifier.AssetId) }, token);
            }
        }

        public async Task<AssetData> GetAssetAsync(AssetIdentifier assetIdentifier, CancellationToken token)
        {
            var asset = await InternalGetAssetAsync(assetIdentifier, token);
            return await Map(asset, token);
        }

        public async Task<AssetData> GetLatestAssetVersionAsync(AssetIdentifier assetIdentifier,
            CancellationToken token)
        {
            if (assetIdentifier == null)
            {
                return null;
            }

            var projectDescriptor = Map(assetIdentifier.ProjectIdentifier);

            try
            {
                var assetId = new AssetId(assetIdentifier.AssetId);
                var asset = await AssetRepository.GetAssetAsync(projectDescriptor, assetId, "Latest", token);
                return await Map(asset, token);
            }
            catch (NotFoundException)
            {
                try
                {
                    Utilities.DevLog($"Latest version not found, fetching the latest version in descending order (slower): {assetIdentifier.AssetId}");

                    var filter = new Unity.Cloud.AssetsEmbedded.AssetSearchFilter();
                    filter.Include().Id.WithValue(assetIdentifier.AssetId);

                    var enumerator = AssetRepository.QueryAssets(new[] { projectDescriptor })
                        .SelectWhereMatchesFilter(filter)
                        .LimitTo(new Range(0, 1))
                        .ExecuteAsync(token)
                        .GetAsyncEnumerator(token);

                    var asset = await enumerator.MoveNextAsync() ? enumerator.Current : default;
                    return await Map(asset, token);
                }
                catch (NotFoundException)
                {
                    return null;
                }
            }
        }

        public async IAsyncEnumerable<AssetData> ListVersionInDescendingOrderAsync(AssetIdentifier assetIdentifier, [EnumeratorCancellation] CancellationToken token)
        {
            if (assetIdentifier == null)
            {
                yield break;
            }

            var project = await AssetRepository.GetAssetProjectAsync(Map(assetIdentifier.ProjectIdentifier), token);
            await foreach (var version in ListVersionInDescendingOrderAsync(project, assetIdentifier, Range.All, token))
            {
                yield return Map(version);
            }
        }

        async IAsyncEnumerable<IAsset> ListVersionInDescendingOrderAsync(IAssetProject project, AssetIdentifier assetIdentifier, Range range, [EnumeratorCancellation] CancellationToken token)
        {
            var asset = await project.GetAssetAsync(new AssetId(assetIdentifier.AssetId), token);
            if (asset == null)
            {
                yield break;
            }

            await foreach (var version in asset.QueryVersions()
                               .OrderBy("versionNumber", Unity.Cloud.AssetsEmbedded.SortingOrder.Descending)
                               .LimitTo(range)
                               .ExecuteAsync(token))
            {
                yield return version;
            }
        }

        public async Task<AssetData> FindAssetAsync(string organizationId, string assetId, string assetVersion,
            CancellationToken token)
        {
            if (string.IsNullOrEmpty(assetId))
            {
                return null;
            }

            var filter = new Cloud.AssetsEmbedded.AssetSearchFilter();
            filter.Include().Id.WithValue(assetId);
            filter.Include().Version.WithValue(assetVersion);

            var asset = await FindAssetAsync(new OrganizationId(organizationId), filter, token);
            return await Map(asset, token);
        }

        async Task<IAsset> FindAssetAsync(OrganizationId organizationId, Cloud.AssetsEmbedded.AssetSearchFilter filter,
            CancellationToken token)
        {
            var query = AssetRepository.QueryAssets(organizationId)
                .SelectWhereMatchesFilter(filter)
                .LimitTo(new Range(0, 1))
                .ExecuteAsync(token);

            var assets = await query.ToListAsync(token);
            return assets.FirstOrDefault();
        }

        public async Task<bool> AssetExistsOnCloudAsync(BaseAssetData assetData, CancellationToken token)
        {
            var res = await CompareAssetWithCloudAsync(assetData, token);
            return res != AssetComparisonResult.NotFoundOrInaccessible;
        }

        public async Task<AssetComparisonResult> CompareAssetWithCloudAsync(BaseAssetData assetData, CancellationToken token)
        {
            if (assetData == null)
            {
                return AssetComparisonResult.Unknown;
            }

            try
            {
                var assetIdentifier = assetData.Identifier;
                var cloudAsset = await GetLatestAssetVersionAsync(assetIdentifier, token);

                if (cloudAsset == null)
                {
                    return AssetComparisonResult.NotFoundOrInaccessible;
                }

                // Even if cloudAsset is != null, we might need to check if the project is archived or not.

                return assetIdentifier == cloudAsset.Identifier && assetData.Updated != null && assetData.Updated == cloudAsset.Updated
                    ? AssetComparisonResult.UpToDate
                    : AssetComparisonResult.OutDated;
            }
            catch (ForbiddenException)
            {
                return AssetComparisonResult.NotFoundOrInaccessible;
            }
            catch (HttpRequestException)
            {
                // Ignore unreachable host
                return AssetComparisonResult.Unknown;
            }
        }

        public async IAsyncEnumerable<AssetIdentifier> GetDependenciesAsync(AssetIdentifier assetIdentifier, Range range,
            [EnumeratorCancellation] CancellationToken token)
        {
            var assetDescriptor = Map(assetIdentifier);
            var project = await AssetRepository.GetAssetProjectAsync(assetDescriptor.ProjectDescriptor, token);

            await foreach (var reference in GetDependenciesAsync(project, assetDescriptor, range,
                               AssetReferenceSearchFilter.Context.Source, token))
            {
                var referenceIdentifier = await FindAssetIdentifierAsync(project, reference, token);
                if (referenceIdentifier != null)
                {
                    yield return referenceIdentifier;
                }
            }
        }

        async Task<AssetIdentifier> FindAssetIdentifierAsync(IAssetProject project, IAssetReference reference,
            CancellationToken token)
        {
            // Referenced by version
            if (reference.TargetAssetVersion.HasValue)
            {
                return new AssetIdentifier(project.Descriptor.OrganizationId.ToString(),
                    project.Descriptor.ProjectId.ToString(),
                    reference.TargetAssetId.ToString(),
                    reference.TargetAssetVersion.Value.ToString());
            }

            // Referenced by label
            if (!string.IsNullOrEmpty(reference.TargetLabel))
            {
                try
                {
                    // Try to fetch the asset from the current project
                    var asset = await project.GetAssetAsync(reference.TargetAssetId, reference.TargetLabel, token);
                    return Map(asset.Descriptor);
                }
                catch (NotFoundException)
                {
                    // Continue to search for the asset in the entire organization
                }

                var filter = new Cloud.AssetsEmbedded.AssetSearchFilter();
                filter.Include().Id.WithValue(reference.TargetAssetId.ToString());
                filter.Include().Labels.WithValue(reference.TargetLabel);

                var result = await FindAssetAsync(project.Descriptor.OrganizationId, filter, token);
                return result != null ? Map(result.Descriptor) : null;
            }

            return null;
        }

        async IAsyncEnumerable<IAssetReference> GetDependenciesAsync(IAssetProject project, AssetDescriptor assetDescriptor, Range range, AssetReferenceSearchFilter.Context context, [EnumeratorCancellation] CancellationToken token)
        {
            var filter = new AssetReferenceSearchFilter();
            filter.AssetVersion.WhereEquals(assetDescriptor.AssetVersion);
            filter.ReferenceContext.WhereEquals(context);

            var query = project.QueryAssetReferences(assetDescriptor.AssetId)
                .SelectWhereMatchesFilter(filter)
                .LimitTo(range)
                .ExecuteAsync(token);

            await foreach(var reference in query)
            {
                if (!reference.IsValid)
                {
                    continue;
                }

                yield return reference;
            }
        }

        public async IAsyncEnumerable<AssetIdentifier> GetDependentsAsync(AssetIdentifier assetIdentifier, Range range, [EnumeratorCancellation] CancellationToken token)
        {
            var assetDescriptor = Map(assetIdentifier);
            var project = await AssetRepository.GetAssetProjectAsync(assetDescriptor.ProjectDescriptor, token);

            await foreach(var reference in GetDependenciesAsync(project, assetDescriptor, range, AssetReferenceSearchFilter.Context.Target, token))
            {
                yield return new AssetIdentifier(assetIdentifier.OrganizationId,
                    assetIdentifier.ProjectId,
                    reference.SourceAssetId.ToString(),
                    reference.SourceAssetVersion.ToString());
            }
        }

        public async Task UpdateDependenciesAsync(AssetIdentifier assetIdentifier, IEnumerable<AssetIdentifier> assetDependencies,
            CancellationToken token)
        {
            var assetDescriptor = Map(assetIdentifier);
            var project = await AssetRepository.GetAssetProjectAsync(assetDescriptor.ProjectDescriptor, token);
            var asset = await AssetRepository.GetAssetAsync(assetDescriptor, token);

            // For instance:
            // Current dependencies = d1, d2, d3
            // New dependencies = d1, d4
            // Dependencies to remove from target asset: d2, d3
            // Dependencies to add to target asset: d4
            var assetDependenciesList = assetDependencies.Select(Map).ToList();

            var tasks = new List<Task>();

            // Remove all dependencies that are not explicitly ignored.
            // This will remove d1 from assetDependenciesList
            // And remove d2, d3 from A
            await foreach (var existingDependency in GetDependenciesAsync(project, assetDescriptor, Range.All,
                               AssetReferenceSearchFilter.Context.Source, token)) // For each d1, d2, d3
            {
                // Remove existing dependencies from the input list.
                // d1 will be removed
                var nb = assetDependenciesList.RemoveAll(dep =>
                    dep.AssetId == existingDependency.TargetAssetId &&
                    dep.AssetVersion == existingDependency.TargetAssetVersion);

                // If this dependency is not in the input list, remove it from target asset
                // d2 and d3 will be removed
                if (nb == 0)
                {
                    tasks.Add(asset.RemoveReferenceAsync(existingDependency.ReferenceId, token));
                }
            }

            // Add any remaining dependencies
            // d4 will be added
            tasks.AddRange(assetDependenciesList.Select(dependencyDescriptor => asset.AddReferenceAsync(dependencyDescriptor, token)));

            await Task.WhenAll(tasks);
        }

        public Task EnableProjectAsync(CancellationToken token = default)
        {
            var projectDescriptor = new ProjectDescriptor(new OrganizationId(m_UnityConnectProxy.OrganizationId),
                new ProjectId(m_UnityConnectProxy.ProjectId));
            return AssetRepository.EnableProjectForAssetManagerAsync(projectDescriptor, token);
        }

        public async Task CreateCollectionAsync(CollectionInfo collectionInfo, CancellationToken token)
        {
            var collectionDescriptor = Map(collectionInfo);
            var project = await AssetRepository.GetAssetProjectAsync(collectionDescriptor.ProjectDescriptor, token);
            var collectionCreation = new AssetCollectionCreation(collectionInfo.Name, k_DefaultCollectionDescription)
            {
                ParentPath = collectionInfo.ParentPath
            };
            await project.CreateCollectionAsync(collectionCreation, token);
        }

        public async Task DeleteCollectionAsync(CollectionInfo collectionInfo, CancellationToken token)
        {
            var collectionDescriptor = Map(collectionInfo);
            var project = await AssetRepository.GetAssetProjectAsync(collectionDescriptor.ProjectDescriptor, token);
            await project.DeleteCollectionAsync(collectionDescriptor.Path, token);
        }

        public async Task RenameCollectionAsync(CollectionInfo collectionInfo, string newName, CancellationToken token)
        {
            var collection = await AssetRepository.GetAssetCollectionAsync(Map(collectionInfo), token);
            var assetCollectionUpdate = new AssetCollectionUpdate { Name = newName, Description = k_DefaultCollectionDescription };
            await collection.UpdateAsync(assetCollectionUpdate, token);
        }

        public async IAsyncEnumerable<IMemberInfo> GetOrganizationMembersAsync(string organizationId, Range range, [EnumeratorCancellation] CancellationToken token)
        {
            var datasetOrganization = await Services.OrganizationRepository.GetOrganizationAsync(new OrganizationId(organizationId));
            await foreach (var memberInfo in datasetOrganization.ListMembersAsync(range, token))
            {
                yield return memberInfo;
            }
        }

        async IAsyncEnumerable<IAssetProject> GetCurrentUserProjectList(string organizationId, Range range,
            [EnumeratorCancellation] CancellationToken token)
        {
            // First call to backend requires a valid authentication state
            while (Services.AuthenticationState != Unity.Cloud.IdentityEmbedded.AuthenticationState.LoggedIn)
            {
                await Task.Delay(200, token);
            }

            await foreach (var project in AssetRepository.ListAssetProjectsAsync(new OrganizationId(organizationId), range, token))
            {
                yield return project;
            }
        }

        public async Task<IOrganization> GetOrganizationAsync(string organizationId)
        {
            try
            {
                return await Services.OrganizationRepository.GetOrganizationAsync(new OrganizationId(organizationId));
            }
            catch (Exception)
            {
                // Patch for fixing UnityEditorCloudServiceAuthorizer not serializing properly.
                // This case only shows up in 2021 because organization is fetched earlier than other versions
                // Delete this code once Identity get bumped beyond 1.2.0-exp.1
            }

            return null;
        }

        async IAsyncEnumerable<AssetData> SearchAsync(string organizationId, IEnumerable<string> projectIds,
            IAssetSearchFilter assetSearchFilter, string sortField, Cloud.AssetsEmbedded.SortingOrder sortingOrder, Range range,
            [EnumeratorCancellation] CancellationToken token)
        {
            var strongTypedOrgId = new OrganizationId(organizationId);
            var projectDescriptors = projectIds
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(id => new ProjectDescriptor(strongTypedOrgId, new ProjectId(id)))
                .ToList();

            if (projectDescriptors.Count == 0)
            {
                yield break;
            }

            var assetQueryBuilder = AssetRepository.QueryAssets(projectDescriptors)
                .LimitTo(range)
                .SelectWhereMatchesFilter(assetSearchFilter)
                .OrderBy(sortField, sortingOrder);

            await foreach (var asset in assetQueryBuilder.ExecuteAsync(token))
            {
                yield return asset == null ? null : Map(asset);
            }
        }

        async Task<IDataset> GetPreviewDatasetAsync(AssetData assetData, CancellationToken token)
        {
            var asset = await InternalGetAssetAsync(assetData, token);
            if (asset == null)
            {
                return null;
            }

            return await asset.GetPreviewDatasetAsync(token);
        }

        async Task<IDataset> GetSourceDatasetAsync(AssetData assetData, CancellationToken token)
        {
            var asset = await InternalGetAssetAsync(assetData, token);
            if (asset == null)
            {
                return null;
            }

            return await asset.GetSourceDatasetAsync(token);
        }

        public async Task UpdateAsync(AssetData assetData, AssetUpdate assetUpdate, CancellationToken token)
        {
            var cloudAssetUpdate = Map(assetUpdate);
            var asset = await InternalGetAssetAsync(assetData.Identifier, token);
            if (asset == null)
            {
                return;
            }
            await asset.UpdateAsync(cloudAssetUpdate, token);
        }

        public async Task UpdateStatusAsync(AssetData assetData, string statusName, CancellationToken token)
        {
            var asset = await InternalGetAssetAsync(assetData.Identifier, token);
            if (asset == null)
            {
                return;
            }
            await asset.UpdateStatusAsync(statusName, token);
        }

        public async Task FreezeAsync(AssetData assetData, string changeLog, CancellationToken token)
        {
            var asset = await InternalGetAssetAsync(assetData.Identifier, token);
            if (asset == null)
            {
                return;
            }

            var assetFreeze = new AssetFreeze(changeLog);
            await asset.FreezeAsync(assetFreeze, token);
        }

        public async Task<Uri> GetPreviewUrlAsync(AssetData assetData, int maxDimension, CancellationToken token)
        {
            try
            {
                var previewFilePath = assetData.PreviewFilePath;

                // [Backwards Compatability] If the preview file path has not been set, we need to fetch it from the asset
                if (string.IsNullOrEmpty(previewFilePath))
                {
                    previewFilePath = await GetPreviewFilePathAsync(assetData, token);
                }

                if (previewFilePath == "/")
                {
                    return null;
                }

                var assetDescriptor = Map(assetData.Identifier);

                var indexOfFirstSlash = previewFilePath.IndexOf("/");
                var datasetId = previewFilePath[..indexOfFirstSlash];
                var filePath = previewFilePath.Substring(indexOfFirstSlash + 1, previewFilePath.Length - datasetId.Length - 1);

                var fileDescriptor = new FileDescriptor(new DatasetDescriptor(assetDescriptor, new DatasetId(datasetId)), filePath);

                var file = await AssetRepository.GetFileAsync(fileDescriptor, token);
                if (file == null)
                {
                    return null;
                }

                return await file.GetResizedImageDownloadUrlAsync(maxDimension, token);
            }
            catch (NotFoundException)
            {
                // Ignore if the preview is not found
            }

            return null;
        }

        async Task<string> GetPreviewFilePathAsync(AssetData assetData, CancellationToken token)
        {
            var asset = await InternalGetAssetAsync(assetData.Identifier, token);
            return asset == null ? string.Empty : Map(asset.PreviewFileDescriptor);
        }

        async Task<List<IMetadata>> GetMetadataAsync(IReadOnlyMetadataContainer metadataContainer, CancellationToken token = default)
        {
            if (metadataContainer == null)
            {
                return null;
            }

            var projectOrganizationProvider = ServicesContainer.instance.Resolve<IProjectOrganizationProvider>();
            var fieldDefinitions = projectOrganizationProvider.SelectedOrganization.MetadataFieldDefinitions;

            var metadataQuery = metadataContainer.Query().ExecuteAsync(token);
            var metadataFields = new List<IMetadata>();
            await foreach (var result in metadataQuery.WithCancellation(token))
            {
                var fieldDefinition = fieldDefinitions.Find(x => x.Key == result.Key);

                if (fieldDefinition == null)
                    continue;

                IMetadata value = fieldDefinition.Type switch
                {
                    MetadataFieldType.Text => new TextMetadata(fieldDefinition.DisplayName, result.Value.AsText().Value),
                    MetadataFieldType.Boolean => new BooleanMetadata(fieldDefinition.DisplayName, result.Value.AsBoolean().Value),
                    MetadataFieldType.Number => new NumberMetadata(fieldDefinition.DisplayName, result.Value.AsNumber().Value),
                    MetadataFieldType.Url => new UrlMetadata(fieldDefinition.DisplayName, result.Value.AsUrl().Uri),
                    MetadataFieldType.Timestamp => new TimestampMetadata(fieldDefinition.DisplayName, result.Value.AsTimestamp().Value),
                    MetadataFieldType.User => new UserMetadata(fieldDefinition.DisplayName, result.Value.AsUser().UserId.ToString()),
                    MetadataFieldType.Selection => result.Value.ValueType == MetadataValueType.MultiSelection
                    ? new MultiSelectionMetadata(fieldDefinition.DisplayName, result.Value.AsMultiSelection().SelectedValues)
                    : new SingleSelectionMetadata(fieldDefinition.DisplayName, result.Value.AsSingleSelection().SelectedValue),
                    _ => null
                };

                if (value != null)
                    metadataFields.Add(value);
            }

            return metadataFields;
        }

        public async Task RemoveThumbnail(AssetData assetData, CancellationToken token)
        {
            var dataset = await GetPreviewDatasetAsync(assetData, token);
            if (dataset != null)
            {
                try
                {
                    await dataset.RemoveFileAsync(k_ThumbnailFilename, token);
                }
                catch (NotFoundException)
                {
                    // Ignore if the file is not found
                }
            }
        }

        public async Task<AssetDataFile> UploadFile(AssetData assetData, string destinationPath, Stream stream, IProgress<HttpProgress> progress, CancellationToken token)
        {
            if (assetData == null || string.IsNullOrEmpty(destinationPath) || stream == null)
            {
                return null;
            }

            AssetDataFile result = null;

            var dataset = await GetSourceDatasetAsync(assetData, token);
            if (dataset != null)
            {
                var file = await UploadFileToDataset(dataset, destinationPath, stream, progress, token);
                if (file != null)
                {
                    result = new AssetDataFile(file);
                }
            }

            return result;
        }

        public async Task RemoveAllFiles(AssetData assetData, CancellationToken token)
        {
            var dataset = await GetSourceDatasetAsync(assetData, token);
            if (dataset != null)
            {
                var filesToWipe = new List<IFile>();
                await foreach (var file in dataset.ListFilesAsync(Range.All, token))
                {
                    filesToWipe.Add(file);
                }

                try
                {
                    await TaskUtils.RunWithMaxConcurrentTasksAsync(filesToWipe, token,
                        file => dataset.RemoveFileAsync(file.Descriptor.Path, token), k_MaxConcurrentFileDeleteTasks);
                }
                catch (NotFoundException)
                {
                    // Ignore if the file is not found
                }
            }
        }

        public async IAsyncEnumerable<AssetDataFile> ListFilesAsync(AssetData assetData, Range range, [EnumeratorCancellation] CancellationToken token)
        {
            IDataset sourceDataset = null;

            try
            {
                sourceDataset = await GetSourceDatasetAsync(assetData, token);
            }
            catch (NotFoundException)
            {
                // Ignore if the dataset is not found
            }

            if (sourceDataset != null)
            {
                await foreach (var file in sourceDataset.ListFilesAsync(range, token))
                {
                    yield return new AssetDataFile(file);
                }
            }
        }

        public async Task<AssetDataFile> UploadThumbnail(AssetData assetData, Texture2D thumbnail, IProgress<HttpProgress> progress, CancellationToken token)
        {
            if (assetData == null || thumbnail == null)
            {
                return null;
            }

            AssetDataFile result = null;
            byte[] bytes = null;
            try
            {
                bytes = thumbnail.EncodeToPNG();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Unable to encode thumbnail before uploading it. Error message is \"{e.Message}\"");
                throw;
            }

            using var stream = new MemoryStream(bytes);
            var dataset = await GetPreviewDatasetAsync(assetData, token);
            if (dataset != null)
            {
                var file = await UploadFileToDataset(dataset, k_ThumbnailFilename, stream, progress, token);
                if (file != null)
                {
                    if (m_SettingsManager.IsTagsCreationUploadEnabled)
                    {
                        await GenerateAndAssignTags(file, token);
                    }
                    result = new AssetDataFile(file);
                }
            }

            return result;
        }

        internal async Task GenerateAndAssignTags(IFile file, CancellationToken token)
        {
            var generatedTags = await file.GenerateSuggestedTagsAsync(token);

            var tags = new List<string>();
            foreach (var tag in generatedTags)
            {
                if (tag.Confidence < m_SettingsManager.TagsConfidenceThreshold)
                {
                    continue;
                }

                tags.Add(tag.Value);
            }

            if (tags.Count > 0)
            {
                var existingTags = file.Tags ?? Array.Empty<string>();
                var fileUpdate = new FileUpdate
                {
                    Tags = existingTags.Union(tags).ToArray()
                };
                await file.UpdateAsync(fileUpdate, token);
                await file.RefreshAsync(token);
            }
        }

        async Task<IFile> UploadFileToDataset(IDataset dataset, string destinationPath, Stream stream, IProgress<HttpProgress> progress, CancellationToken token)
        {
            if (dataset == null || string.IsNullOrEmpty(destinationPath) || stream == null)
            {
                return null;
            }

            IFile file = null;
            var fileCreation = new FileCreation(destinationPath.Replace('\\', '/')) // Backend doesn't support backslashes AMECO-2616
            {
                // Preview transformation prevents us from freezing the asset or cause unwanted modification in the asset. Remove this line when Preview will not affect the asset anymore AMECO-2759
                DisableAutomaticTransformations = true
            };

            try
            {
                file = await dataset.UploadFileAsync(fileCreation, stream, progress, token);
            }
            catch (ServiceException e)
            {
                if (e.StatusCode != HttpStatusCode.Conflict)
                {
                    UnityEngine.Debug.LogError($"Unable to upload file {destinationPath} to dataset {dataset.Name}. Error code is {e.ErrorCode} with message \"{e.Message}\"");
                    throw;
                }
            }

            return file;
        }

        public async Task<IReadOnlyDictionary<string, Uri>> GetAssetDownloadUrlsAsync(AssetData assetData, IProgress<FetchDownloadUrlsProgress> progress, CancellationToken token)
        {
            var asset = await InternalGetAssetAsync(assetData, token);
            if (asset == null)
            {
                return null;
            }

            var result = new Dictionary<string, Uri>();

            // We'll need the Source dataset in all cases so might grab it first
            var sourceDataset = await GetSourceDatasetAsync(assetData, token);
            if (sourceDataset == null)
            {
                return result;
            }

            progress?.Report(new FetchDownloadUrlsProgress("Identifying url access strategy", 0.0f));

#pragma warning disable 618
            // Although the method is obsolete, we still need to use it to count the number of files in the asset
            // this can be removed once V2 end points become available as we will be able to simplify to use only GetAssetDownloadUrlsSingleRequestAsync with proper pagination

            // Count the asset because if the asset has < threshold files, we can retrieve all urls in a single calls
            int fileCount = 0;
            await foreach (var _ in asset.ListFilesAsync(..(k_MaxNumberOfFilesForAssetDownloadUrlFetch + 1), token))
            {
                fileCount++;
            }
#pragma warning restore 618

            if (fileCount > k_MaxNumberOfFilesForAssetDownloadUrlFetch)
            {
                // Slow path, request urls per file
                result = await GetAssetDownloadUrlsMultipleRequestsAsync(sourceDataset, progress, token);
            }
            else
            {
                // Fast track, request urls in a single call
                result = await GetAssetDownloadUrlsSingleRequestAsync(asset, sourceDataset, progress, token);
            }

            return result;
        }

        async Task<Dictionary<string, Uri>> GetAssetDownloadUrlsSingleRequestAsync(IAsset asset, IDataset sourceDataset, IProgress<FetchDownloadUrlsProgress> progress, CancellationToken token)
        {
            if (asset == null || sourceDataset == null)
            {
                return null;
            }

            var result = new Dictionary<string, Uri>();

            // Requesting all files url for the whole asset.
            progress?.Report(new FetchDownloadUrlsProgress("Downloading all urls at once", 0.2f));
            var downloadUrls = await asset.GetAssetDownloadUrlsAsync(token);
            foreach (var (filePath, url) in downloadUrls)
            {
                // Check that the file is from the Source dataset (to be changed for something more robust)
                // or is from a UVCS source
                if (!url.ToString().Contains(sourceDataset.Descriptor.DatasetId.ToString()) &&
                    !url.ToString().Contains(k_UVCSUrl))
                {
                    continue;
                }

                if (MetafilesHelper.IsOrphanMetafile(filePath, downloadUrls.Keys))
                {
                    continue;
                }

                if (AssetDataDependencyHelper.IsASystemFile(filePath))
                {
                    continue;
                }

                result[filePath] = url;
            }
            progress?.Report(new FetchDownloadUrlsProgress("Completed downloading urls", 1.0f));

            return result;
        }

        async Task<Dictionary<string, Uri>> GetAssetDownloadUrlsMultipleRequestsAsync(IDataset sourceDataset, IProgress<FetchDownloadUrlsProgress> progress, CancellationToken token)
        {
            var result = new Dictionary<string, Uri>();

            if (sourceDataset == null)
            {
                return result;
            }

            progress?.Report(new FetchDownloadUrlsProgress("Downloading all urls one by one", 0.0f));
            // The previous request to list all files was on the whole asset since we needed to ensure that the bulk urls fetch
            // (for the fast track) was below a threshold. Now that we are on the slow path, let's work on the "Source" dataset only
            var files = new List<IFile>(k_MaxNumberOfFilesForAssetDownloadUrlFetch * 2);
            await foreach (var file in sourceDataset.ListFilesAsync(Range.All, token))
            {
                files.Add(file);
            }

            for (int i = 0; i < files.Count; ++i)
            {
                var file = files[i];
                if (MetafilesHelper.IsOrphanMetafile(file.Descriptor.Path, files.Select(f => f.Descriptor.Path)))
                {
                    continue;
                }

                if (AssetDataDependencyHelper.IsASystemFile(file.Descriptor.Path))
                {
                    continue;
                }

                progress?.Report(new FetchDownloadUrlsProgress(Path.GetFileName(file.Descriptor.Path), (float)i / files.Count));

                var url = await file.GetDownloadUrlAsync(token);
                result[file.Descriptor.Path] = url;
            }
            progress?.Report(new FetchDownloadUrlsProgress("Completed downloading urls", 1.0f));

            return result;
        }

        public AssetData DeserializeAssetData(string content)
        {
            // If the Cloud SDKs have been enbedded, we need to modify the namespaces
            if (typeof(IAsset).FullName != "Unity.Cloud.Assets.IAsset")
            {
                var updatedNamespace = typeof(IAsset).FullName.Replace("IAsset", string.Empty);
                content = content.Replace("Unity.Cloud.Assets.", updatedNamespace);
            }

            var asset = AssetRepository.DeserializeAsset(content);
            return asset == null ? null : Map(asset);
        }

        async Task<AssetData> Map(IAsset asset, CancellationToken token)
        {
            if (asset == null)
            {
                return null;
            }

            var data = Map(asset);

            data.Metadata = await GetMetadataAsync(asset.Metadata as IReadOnlyMetadataContainer, token);

            return data;
        }

        static AssetData Map(IAsset asset)
        {
            if (asset == null)
            {
                return null;
            }

            return new AssetData(
                Map(asset.Descriptor),
                asset.FrozenSequenceNumber,
                asset.ParentFrozenSequenceNumber,
                asset.Changelog,
                asset.Name,
                Map(asset.Type),
                asset.StatusName,
                asset.Description,
                asset.AuthoringInfo.Created,
                asset.AuthoringInfo.Updated,
                asset.AuthoringInfo.CreatedBy.ToString(),
                asset.AuthoringInfo.UpdatedBy.ToString(),
                Map(asset.PreviewFileDescriptor),
                asset.State == AssetState.Frozen,
                asset.Tags);

        }

        static AssetType Map(Unity.Cloud.AssetsEmbedded.AssetType assetType)
        {
            return assetType switch
            {
                Cloud.AssetsEmbedded.AssetType.Asset_2D => AssetType.Asset2D,
                Cloud.AssetsEmbedded.AssetType.Model_3D => AssetType.Model3D,
                Cloud.AssetsEmbedded.AssetType.Audio => AssetType.Audio,
                Cloud.AssetsEmbedded.AssetType.Material => AssetType.Material,
                Cloud.AssetsEmbedded.AssetType.Script => AssetType.Script,
                Cloud.AssetsEmbedded.AssetType.Video => AssetType.Video,
                Cloud.AssetsEmbedded.AssetType.Unity_Editor => AssetType.UnityEditor,
                _ => AssetType.Other
            };
        }

        static AssetIdentifier Map(AssetDescriptor descriptor)
        {
            return new AssetIdentifier(descriptor.OrganizationId.ToString(),
                descriptor.ProjectId.ToString(),
                descriptor.AssetId.ToString(),
                descriptor.AssetVersion.ToString());
        }

        static AssetDescriptor Map(AssetIdentifier assetIdentifier)
        {
            return new AssetDescriptor(
                new ProjectDescriptor(
                    new OrganizationId(assetIdentifier.OrganizationId),
                    new ProjectId(assetIdentifier.ProjectId)),
                new AssetId(assetIdentifier.AssetId),
                new AssetVersion(assetIdentifier.Version));
        }

        static ProjectDescriptor Map(ProjectIdentifier projectIdentifier)
        {
            return new ProjectDescriptor(
                new OrganizationId(projectIdentifier.OrganizationId),
                new ProjectId(projectIdentifier.ProjectId));
        }

        static CollectionDescriptor Map(CollectionInfo collectionInfo)
        {
            return new CollectionDescriptor(
                new ProjectDescriptor(
                    new OrganizationId(collectionInfo.OrganizationId),
                    new ProjectId(collectionInfo.ProjectId)),
                new CollectionPath(collectionInfo.GetFullPath()));
        }

        static string Map(FileDescriptor fileDescriptor)
        {
            return string.IsNullOrEmpty(fileDescriptor.Path) ? "/" : $"{fileDescriptor.DatasetId}/{fileDescriptor.Path}";
        }

        static AuthenticationState Map(Unity.Cloud.IdentityEmbedded.AuthenticationState authenticationState) =>
            authenticationState switch
            {
                Unity.Cloud.IdentityEmbedded.AuthenticationState.AwaitingInitialization => AuthenticationState.AwaitingInitialization,
                Unity.Cloud.IdentityEmbedded.AuthenticationState.AwaitingLogin => AuthenticationState.AwaitingLogin,
                Unity.Cloud.IdentityEmbedded.AuthenticationState.LoggedIn => AuthenticationState.LoggedIn,
                Unity.Cloud.IdentityEmbedded.AuthenticationState.AwaitingLogout => AuthenticationState.AwaitingLogout,
                Unity.Cloud.IdentityEmbedded.AuthenticationState.LoggedOut => AuthenticationState.LoggedOut,
                _ => throw new ArgumentOutOfRangeException(nameof(authenticationState), authenticationState, null)
            };

        static IAssetSearchFilter Map(AssetSearchFilter assetSearchFilter)
        {
            var cloudAssetSearchFilter = new Cloud.AssetsEmbedded.AssetSearchFilter();

            if (assetSearchFilter.CreatedBy != null)
            {
                cloudAssetSearchFilter.Include().AuthoringInfo.CreatedBy.WithValue(assetSearchFilter.CreatedBy);
            }

            if (assetSearchFilter.Status != null)
            {
                cloudAssetSearchFilter.Include().Status.WithValue(assetSearchFilter.Status);
            }

            if (assetSearchFilter.UpdatedBy != null)
            {
                cloudAssetSearchFilter.Include().AuthoringInfo.UpdatedBy.WithValue(assetSearchFilter.UpdatedBy);
            }

            if (assetSearchFilter.UnityType != null)
            {
                var regex = AssetDataTypeHelper.GetRegexForExtensions(assetSearchFilter.UnityType.Value);
                cloudAssetSearchFilter.Include().Files.Path.WithValue(regex);
            }

            if (assetSearchFilter.Collection != null)
            {
                cloudAssetSearchFilter.Collections.WhereContains(new CollectionPath(assetSearchFilter.Collection));
            }

            if (assetSearchFilter.Searches != null)
            {
                var searchString = string.Concat("*", string.Join('*', assetSearchFilter.Searches), "*");
                cloudAssetSearchFilter.Any().Name.WithValue(searchString);
                cloudAssetSearchFilter.Any().Description.WithValue(searchString);
                cloudAssetSearchFilter.Any().Tags.WithValue(searchString);
            }

            return cloudAssetSearchFilter;
        }

        static GroupableField Map(AssetSearchGroupBy groupBy)
        {
            switch (groupBy)
            {
                case AssetSearchGroupBy.Name:
                    return GroupableField.Name;
                case AssetSearchGroupBy.Status:
                    return GroupableField.Status;
                case AssetSearchGroupBy.CreatedBy:
                    return GroupableField.CreatedBy;
                case AssetSearchGroupBy.UpdatedBy:
                    return GroupableField.UpdateBy;
                default:
                    throw new ArgumentOutOfRangeException(nameof(groupBy), groupBy, null);
            }
        }

        static StorageUsage Map(ICloudStorageUsage cloudStorageUsage)
        {
            return new StorageUsage(cloudStorageUsage.UsageBytes, cloudStorageUsage.TotalStorageQuotaBytes);
        }

        static Unity.Cloud.AssetsEmbedded.AssetCreation Map(AssetCreation assetCreation)
        {
            if (assetCreation == null)
            {
                return null;
            }

            return new Unity.Cloud.AssetsEmbedded.AssetCreation(assetCreation.Name)
            {
                Type = Map(assetCreation.Type),
                Collections = assetCreation.Collections?.Select(x => new CollectionPath(x)).ToList(),
                Tags = assetCreation.Tags
            };
        }

        static IAssetUpdate Map(AssetUpdate assetUpdate)
        {
            if (assetUpdate == null)
            {
                return null;
            }

            return new Unity.Cloud.AssetsEmbedded.AssetUpdate()
            {
                Name = assetUpdate.Name,
                Type = Map(assetUpdate.Type),
                PreviewFile = assetUpdate.PreviewFile,
                Tags = assetUpdate.Tags
            };
        }

        static Cloud.AssetsEmbedded.AssetType Map(AssetType assetType)
        {
            return assetType switch
            {
                AssetType.Asset2D => Cloud.AssetsEmbedded.AssetType.Asset_2D,
                AssetType.Model3D => Cloud.AssetsEmbedded.AssetType.Model_3D,
                AssetType.Audio => Cloud.AssetsEmbedded.AssetType.Audio,
                AssetType.Material => Cloud.AssetsEmbedded.AssetType.Material,
                AssetType.Script => Cloud.AssetsEmbedded.AssetType.Script,
                AssetType.Video => Cloud.AssetsEmbedded.AssetType.Video,
                AssetType.UnityEditor => Cloud.AssetsEmbedded.AssetType.Unity_Editor,
                _ => Cloud.AssetsEmbedded.AssetType.Other
            };
        }

        static Cloud.AssetsEmbedded.SortingOrder Map(SortingOrder sortingOrder)
        {
            return sortingOrder switch
            {
                SortingOrder.Ascending => Cloud.AssetsEmbedded.SortingOrder.Ascending,
                SortingOrder.Descending => Cloud.AssetsEmbedded.SortingOrder.Descending,
                _ => throw new ArgumentOutOfRangeException(nameof(sortingOrder), sortingOrder, null)
            };
        }

        static class Services
        {
            static IAssetRepository s_AssetRepository;
            static IServiceHttpClient s_ServiceHttpClient;
            static IServiceHostResolver s_ServiceHostResolver;

            public static IAssetRepository AssetRepository
            {
                get
                {
                    if (s_AssetRepository == null)
                    {
                        CreateServices();
                    }

                    return s_AssetRepository;
                }
            }

            public static IOrganizationRepository OrganizationRepository => UnityEditorServiceAuthorizer.instance;

            public static Unity.Cloud.IdentityEmbedded.AuthenticationState AuthenticationState =>
                UnityEditorServiceAuthorizer.instance.AuthenticationState;

            public static Action AuthenticationStateChanged;

            internal static void InitAuthenticatedServices()
            {
                if (s_AssetRepository == null)
                {
                    CreateServices();
                }
            }

            static void CreateServices()
            {
                var pkgInfo = PackageInfo.FindForAssembly(Assembly.GetAssembly(typeof(Services)));
                var httpClient = new UnityHttpClient();
                s_ServiceHostResolver = UnityRuntimeServiceHostResolverFactory.Create();

                UnityEditorServiceAuthorizer.instance.AuthenticationStateChanged += OnAuthenticationStateChanged;

                s_ServiceHttpClient = new ServiceHttpClient(httpClient, UnityEditorServiceAuthorizer.instance, new AppIdProvider())
                    .WithApiSourceHeaders(pkgInfo.name, pkgInfo.version);

                s_AssetRepository = AssetRepositoryFactory.Create(s_ServiceHttpClient, s_ServiceHostResolver);
            }

            static void OnAuthenticationStateChanged(Unity.Cloud.IdentityEmbedded.AuthenticationState state)
            {
                AuthenticationStateChanged?.Invoke();
            }

            class AppIdProvider : IAppIdProvider
            {
                public AppId GetAppId()
                {
                    return new AppId();
                }
            }
        }
    }
}
