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
        public List<string> AssetIds;
        public List<string> AssetVersions;
        public List<string> CreatedBy;
        public List<string> UpdatedBy;
        public List<string> Status;
        public string Collection;

        [SerializeReference]
        public List<UnityAssetType> UnityTypes;

        [SerializeReference]
        public List<IMetadata> CustomMetadata;
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

        int DefaultSearchPageSize { get; }

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
        Task<string> GetLatestAssetVersionLiteAsync(AssetIdentifier assetIdentifier, CancellationToken token);
        IAsyncEnumerable<AssetData> ListVersionInDescendingOrderAsync(AssetIdentifier assetIdentifier, CancellationToken token);

        IAsyncEnumerable<AssetData> SearchAsync(string organizationId, IEnumerable<string> projectIds,
            AssetSearchFilter assetSearchFilter, SortField sortField, SortingOrder sortingOrder, int startIndex,
            int pageSize, CancellationToken token);
        IAsyncEnumerable<AssetIdentifier> SearchLiteAsync(string organizationId, IEnumerable<string> projectIds,
            AssetSearchFilter assetSearchFilter, SortField sortField, SortingOrder sortingOrder, int startIndex,
            int pageSize, CancellationToken token);
        Task<List<string>> GetFilterSelectionsAsync(string organizationId, IEnumerable<string> projectIds,
            AssetSearchFilter assetSearchFilter,
            AssetSearchGroupBy groupBy, CancellationToken token);
        public Task<List<string>> GetFilterSelectionsAsync(string organizationId, IEnumerable<string> projectIds,
            AssetSearchFilter assetSearchFilter, string metadataField, CancellationToken token);

        Task<AssetData> CreateAssetAsync(ProjectIdentifier projectIdentifier, AssetCreation assetCreation, CancellationToken token);
        Task<AssetData> CreateUnfrozenVersionAsync(AssetData assetData, CancellationToken token);

        Task RemoveAsset(AssetIdentifier assetIdentifier, CancellationToken token);

        Task UpdateAsync(AssetData assetData, AssetUpdate assetUpdate, CancellationToken token);
        Task UpdateStatusAsync(AssetData assetData, string statusName, CancellationToken token);
        Task FreezeAsync(AssetData assetData, string changeLog, CancellationToken token);

        Task<Uri> GetPreviewUrlAsync(AssetData assetData, int maxDimension, CancellationToken token);

        Task<ImportAttribute.ImportStatus> GetImportStatusAsync(BaseAssetData assetData, CancellationToken token);
        Task UpdateImportStatusAsync(IEnumerable<BaseAssetData> assetDatas, CancellationToken token);

        IAsyncEnumerable<AssetIdentifier> GetDependenciesAsync(AssetIdentifier assetIdentifier, Range range, CancellationToken token);
        IAsyncEnumerable<AssetIdentifier> GetDependentsAsync(AssetIdentifier assetIdentifier, Range range, CancellationToken token);
        Task UpdateDependenciesAsync(AssetIdentifier assetIdentifier, IEnumerable<AssetIdentifier> assetDependencies, CancellationToken token);

        // Files

        Task<IReadOnlyDictionary<string, Uri>> GetAssetDownloadUrlsAsync(AssetData assetData, IProgress<FetchDownloadUrlsProgress> progress, CancellationToken token);

        Task<AssetDataFile> UploadThumbnail(AssetData assetData, Texture2D thumbnail, IProgress<HttpProgress> progress, CancellationToken token);
        Task RemoveThumbnail(AssetData assetData, CancellationToken token);

        Task<AssetDataFile> UploadFile(AssetData assetData, string destinationPath, Stream stream, IProgress<HttpProgress> progress, CancellationToken token);
        Task RemoveAllFiles(AssetData assetData, CancellationToken token);
        IAsyncEnumerable<AssetDataFile> ListFilesAsync(AssetIdentifier assetIdentifier, AssetDataset assetDataset, Range range, CancellationToken token);

        // Datasets

        Task<AssetDataset> GetDatasetAsync(AssetData assetData, IEnumerable<string> systemTags, CancellationToken token);
    }

    [Serializable]
    class AssetsSdkProvider : BaseService<IAssetsProvider>, IAssetsProvider, AssetsSdkProvider.IDataMapper
    {
        const string k_ThumbnailFilename = "unity_thumbnail.png";
        const string k_UVCSUrl = "cloud.plasticscm.com";
        const int k_MaxNumberOfFilesForAssetDownloadUrlFetch = 100;
        static readonly int k_MaxConcurrentFileDeleteTasks = 20;
        static readonly string k_DefaultCollectionDescription = "none";
        static readonly string k_SourceDatasetTag = "Source";
        static readonly string k_PreviewDatasetTag = "Preview";

        [SerializeReference]
        IUnityConnectProxy m_UnityConnectProxy;

        [SerializeReference]
        ISettingsManager m_SettingsManager;

        IAssetRepository m_AssetRepositoryOverride;
        IDataMapper m_DataMapperOverride;

        IAssetRepository AssetRepository => m_AssetRepositoryOverride ?? Services.AssetRepository;
        IDataMapper DataMapper => m_DataMapperOverride ?? this;

        public AssetsSdkProvider() { }

        /// <summary>
        /// Internal constructor that allow the IAssetRepository to be overriden. Only used for testing.
        ///
        /// IMPORTANT: Since m_AssetRepositoryOverride does not support domain reload, the AssetsSdkProvider constructed cannot
        /// be used across domain reloads
        /// </summary>
        /// <param name="assetRepository"></param>
        /// <param name="dataMapper"></param>
        internal AssetsSdkProvider(IAssetRepository assetRepository, IDataMapper dataMapper)
        {
            m_AssetRepositoryOverride = assetRepository;
            m_DataMapperOverride = dataMapper;
        }

        [ServiceInjection]
        public void Inject(IUnityConnectProxy unityConnectProxy, ISettingsManager settingsManager)
        {
            m_UnityConnectProxy = unityConnectProxy;
            m_SettingsManager = settingsManager;
        }

        public event Action<AuthenticationState> AuthenticationStateChanged;

        public AuthenticationState AuthenticationState => Map(Services.AuthenticationState);

        public int DefaultSearchPageSize => 99;

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
#if AM4U_DEV
            var t = new Stopwatch();
            t.Start();
#endif

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

                var (projectInfo, hasCollection) = await DataMapper.From(project, token);

                organizationInfo.ProjectInfos.Add(projectInfo);

                if (!hasCollection)
                    continue;

                _ = GetCollections(project,
                    projectInfo.Name,
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

#if AM4U_DEV
            t.Stop();
            Utilities.DevLog($"Fetching {organizationInfo.ProjectInfos.Count} Projects took {t.ElapsedMilliseconds}ms");
#endif

            var filter = new FieldDefinitionSearchFilter();
            filter.Deleted.WhereEquals(false);
            filter.FieldOrigin.WhereEquals(FieldDefinitionOrigin.User);

            var definitionsQuery = AssetRepository.QueryFieldDefinitions(new OrganizationId(organizationId))
                .WithCacheConfiguration(new FieldDefinitionCacheConfiguration {CacheProperties = true})
                .SelectWhereMatchesFilter(filter);

            await foreach (var result in definitionsQuery.ExecuteAsync(token))
            {
                var metadataFieldDefinition = await DataMapper.From(result, token);
                organizationInfo.MetadataFieldDefinitions.Add(metadataFieldDefinition);
            }

            return organizationInfo;
        }

        public async Task<ProjectInfo> GetProjectInfoAsync(string organizationId, string projectId, CancellationToken token)
        {
            var projectDescriptor = new ProjectDescriptor(new OrganizationId(organizationId), new ProjectId(projectId));
            var assetProject = await AssetRepository.GetAssetProjectAsync(projectDescriptor, token);

            var (projectInfo, hasCollection) = await DataMapper.From(assetProject, token);

            if (hasCollection)
            {
                await GetCollections(assetProject,
                    projectInfo.Name,
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
            }

            return projectInfo;
        }

        static Task GetCollections(IAssetProject project, string projectName, Action<IEnumerable<IAssetCollection>> successCallback, CancellationToken token)
        {
            var asyncLoad = new AsyncLoadOperation();
            return asyncLoad.Start(t => project.ListCollectionsAsync(Range.All, CancellationTokenSource.CreateLinkedTokenSource(t, token).Token),
                null,
                successCallback,
                null,
                () => Utilities.DevLog($"Cancelled fetching collections for {projectName}."),
                ex => Utilities.DevLog($"Error fetching collections for {projectName}: {ex.Message}"));
        }

        public IAsyncEnumerable<IOrganization> ListOrganizationsAsync(Range range, CancellationToken token)
        {
            return Services.OrganizationRepository.ListOrganizationsAsync(range, token);
        }

        public async IAsyncEnumerable<AssetData> SearchAsync(string organizationId, IEnumerable<string> projectIds,
            AssetSearchFilter assetSearchFilter, SortField sortField, SortingOrder sortingOrder, int startIndex,
            int pageSize, [EnumeratorCancellation] CancellationToken token)
        {
            var cacheConfiguration = new AssetCacheConfiguration
            {
                CacheProperties = true,
                CacheDatasetList = true,
                DatasetCacheConfiguration = new DatasetCacheConfiguration {CacheProperties = true}
            };

            await foreach (var asset in SearchAsync(new OrganizationId(organizationId), projectIds, assetSearchFilter,
                               sortField, sortingOrder, startIndex, pageSize, cacheConfiguration, token))
            {
                yield return asset == null ? null : await DataMapper.From(asset, token);
            }
        }

        public async IAsyncEnumerable<AssetIdentifier> SearchLiteAsync(string organizationId, IEnumerable<string> projectIds,
            AssetSearchFilter assetSearchFilter, SortField sortField, SortingOrder sortingOrder, int startIndex,
            int pageSize, [EnumeratorCancellation] CancellationToken token)
        {
            await foreach (var asset in SearchAsync(new OrganizationId(organizationId), projectIds, assetSearchFilter,
                               sortField, sortingOrder, startIndex, pageSize, AssetCacheConfiguration.NoCaching, token))
            {
                yield return Map(asset.Descriptor);
            }
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

            var keys = new HashSet<string>();

            await foreach (var kvp in groupAndCountAssetsQueryBuilder.ExecuteAsync((Groupable) cloudGroupBy, token))
            {
                keys.Add(kvp.Key.AsString());
            }

            var sortedList = keys.ToList();
            sortedList.Sort();
            return sortedList;
        }

        public async Task<List<string>> GetFilterSelectionsAsync(string organizationId, IEnumerable<string> projectIds,
            AssetSearchFilter assetSearchFilter, string metadataField, CancellationToken token)
        {
            var cloudAssetSearchFilter = Map(assetSearchFilter);

            var strongTypedOrgId = new OrganizationId(organizationId);
            var projectDescriptors = projectIds.Select(p => new ProjectDescriptor(strongTypedOrgId, new ProjectId(p))).ToList();

            var groupAndCountAssetsQueryBuilder = AssetRepository.GroupAndCountAssets(projectDescriptors).SelectWhereMatchesFilter(cloudAssetSearchFilter);
            var query = groupAndCountAssetsQueryBuilder.ExecuteAsync(Groupable.FromMetadata(MetadataOwner.Asset, metadataField), token);
            var aggregation = new List<string>();
            await foreach(var selection in query)
            {
                aggregation.Add(selection.Key.AsString());
            }

            var result = aggregation.Distinct().ToList();

            result.Sort();

            return result;
        }

        public async Task<AssetData> CreateAssetAsync(ProjectIdentifier projectIdentifier, AssetCreation assetCreation, CancellationToken token)
        {
            var projectDescriptor = Map(projectIdentifier);
            var cloudAssetCreation = Map(assetCreation);
            var project = await AssetRepository.GetAssetProjectAsync(projectDescriptor, token);
            var assetDescriptor = await project.CreateAssetLiteAsync(cloudAssetCreation, token);

            return await Map(assetDescriptor, token);
        }

        public async Task<AssetData> CreateUnfrozenVersionAsync(AssetData assetData, CancellationToken token)
        {
            var asset = await InternalGetAssetAsync(assetData, token);
            if (asset != null)
            {
                var unfrozenVersionDescriptor = await asset.CreateUnfrozenVersionLiteAsync(token);
                return await Map(unfrozenVersionDescriptor, token);
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
            return await AssetRepository.GetAssetAsync(Map(assetIdentifier), token);
        }

        public async Task RemoveAsset(AssetIdentifier assetIdentifier, CancellationToken token)
        {
            if (assetIdentifier == null)
            {
                return;
            }

            var project = await AssetRepository.GetAssetProjectAsync(Map(assetIdentifier.ProjectIdentifier), token);
            await project.UnlinkAssetsAsync(new[] {new AssetId(assetIdentifier.AssetId)}, token);
        }

        public async Task<AssetData> GetAssetAsync(AssetIdentifier assetIdentifier, CancellationToken token)
        {
            return await Map(Map(assetIdentifier), token);
        }

        async Task UpdateMetadata(AssetIdentifier assetIdentifier, List<IMetadata> metadata, CancellationToken token)
        {
            var asset = await InternalGetAssetAsync(assetIdentifier, token);

            var keyToDelete = new List<string>();
            var metadataToAddOrUpdate = metadata.ToDictionary(m => m.FieldKey, Map);

            var metadataQuery = asset.Metadata.Query().ExecuteAsync(token);
            await foreach (var result in metadataQuery)
            {
                if (!metadataToAddOrUpdate.ContainsKey(result.Key))
                {
                    keyToDelete.Add(result.Key);
                }
            }

            await asset.Metadata.RemoveAsync(keyToDelete, token);
            await asset.Metadata.AddOrUpdateAsync(metadataToAddOrUpdate, token);
        }

        public async Task<AssetData> GetLatestAssetVersionAsync(AssetIdentifier assetIdentifier, CancellationToken token)
        {
            var asset = await GetLatestAssetVersionAsync(assetIdentifier, GetAssetCacheConfigurationForMapping(), token);
            return await DataMapper.From(asset, token);
        }

        public async Task<string> GetLatestAssetVersionLiteAsync(AssetIdentifier assetIdentifier, CancellationToken token)
        {
            var asset = await GetLatestAssetVersionAsync(assetIdentifier, AssetCacheConfiguration.NoCaching, token);
            return asset?.Descriptor.AssetVersion.ToString();
        }

        async Task<IAsset> GetLatestAssetVersionAsync(AssetIdentifier assetIdentifier, AssetCacheConfiguration assetCacheConfiguration, CancellationToken token)
        {
            if (assetIdentifier == null)
            {
                return null;
            }

            var projectDescriptor = Map(assetIdentifier.ProjectIdentifier);
            var assetId = new AssetId(assetIdentifier.AssetId);

            try
            {
                var asset = await AssetRepository.GetAssetAsync(projectDescriptor, assetId, "Latest", token);
                return await asset.WithCacheConfigurationAsync(assetCacheConfiguration, token);
            }
            catch (NotFoundException)
            {
                try
                {
                    Utilities.DevLog($"Latest version not found, fetching the latest version in descending order (slower): {assetIdentifier.AssetId}");

                    var project = await AssetRepository.GetAssetProjectAsync(projectDescriptor, token);
                    var enumerator = project.QueryAssetVersions(assetId)
                        .WithCacheConfiguration(assetCacheConfiguration)
                        .LimitTo(new Range(0, 1))
                        .ExecuteAsync(token)
                        .GetAsyncEnumerator(token);

                    return await enumerator.MoveNextAsync() ? enumerator.Current : default;
                }
                catch (NotFoundException)
                {
                    // Ignore, asset could not be found on cloud.
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
            var assetId = new AssetId(assetIdentifier.AssetId);

            var versionQuery = project.QueryAssetVersions(assetId)
                .WithCacheConfiguration(GetAssetCacheConfigurationForMapping())
                .OrderBy("versionNumber", Unity.Cloud.AssetsEmbedded.SortingOrder.Descending);

            await foreach (var version in versionQuery.ExecuteAsync(token))
            {
                yield return await DataMapper.From(version, token);
            }
        }

        async Task<IAsset> FindAssetAsync(OrganizationId organizationId, Cloud.AssetsEmbedded.AssetSearchFilter filter, AssetCacheConfiguration cacheConfiguration, CancellationToken token)
        {
            var enumerator = AssetRepository.QueryAssets(organizationId)
                .SelectWhereMatchesFilter(filter)
                .WithCacheConfiguration(cacheConfiguration)
                .LimitTo(new Range(0, 1))
                .ExecuteAsync(token)
                .GetAsyncEnumerator(token);

            return await enumerator.MoveNextAsync() ? enumerator.Current : default;
        }

        public async Task UpdateImportStatusAsync(IEnumerable<BaseAssetData> assetDatas, CancellationToken token)
        {
            // Split the searches by organization
            var assetsByOrg = new Dictionary<string, List<BaseAssetData>>();
            foreach (var assetData in assetDatas)
            {
                if (string.IsNullOrEmpty(assetData.Identifier.OrganizationId))
                    continue;

                if (!assetsByOrg.ContainsKey(assetData.Identifier.OrganizationId))
                {
                    assetsByOrg.Add(assetData.Identifier.OrganizationId, new List<BaseAssetData>());
                }

                assetsByOrg[assetData.Identifier.OrganizationId].Add(assetData);
            }

            if (assetsByOrg.Count > 1)
            {
                Utilities.DevLog("Initiating search in multiple organizations.");
            }

            await Task.WhenAll(assetsByOrg.Select(kvp => UpdateImportStatusAsync(kvp.Key, kvp.Value, token)));
        }

        Task UpdateImportStatusAsync(string organizationId, List<BaseAssetData> assetDatas, CancellationToken token)
        {
            // Split the asset list into chunks for multiple searches.

            var strongTypedOrganizationId = new OrganizationId(organizationId);

            var tasks = new List<Task>();
            var startIndex = 0;
            while (startIndex < assetDatas.Count)
            {
                var maxCount = Math.Min(DefaultSearchPageSize, assetDatas.Count - startIndex);
                tasks.Add(UpdateImportStatusAsync(strongTypedOrganizationId, assetDatas.GetRange(startIndex, maxCount), token));
                startIndex += DefaultSearchPageSize;
            }

            return Task.WhenAll(tasks);
        }

        async Task UpdateImportStatusAsync(OrganizationId organizationId, List<BaseAssetData> assetDatas, CancellationToken token)
        {
            // If there is only 1 asset, fetch that asset info directly (search has more overhead than a direct fetch).
            if (assetDatas.Count == 1)
            {
                var assetData = assetDatas[0];
                var result = await GetImportStatusAsync(assetData, token);
                assetData.AssetDataAttributeCollection = new AssetDataAttributeCollection(new ImportAttribute(result));
                return;
            }

            // Clear any previous attribute
            foreach (var baseAssetData in assetDatas)
            {
                baseAssetData.ResetAssetDataAttributes();
            }

            // When there are multiple assets, initiate a search to batch asset infos.
            var searchFilter = new Unity.Cloud.AssetsEmbedded.AssetSearchFilter();

            var assetIds = assetDatas.Select(x => new AssetId(x.Identifier.AssetId)).ToArray();
            searchFilter.Include().Id.WithValue(string.Join(' ', assetIds));

            var query = AssetRepository.QueryAssets(organizationId)
                .SelectWhereMatchesFilter(searchFilter)
                .WithCacheConfiguration(new AssetCacheConfiguration
                {
                    CacheProperties = true
                });

            try
            {
                await foreach (var asset in query.ExecuteAsync(token))
                {
                    var identifier = Map(asset.Descriptor);
                    var assetData = assetDatas.Find(x =>
                        x.Identifier.OrganizationId == identifier.OrganizationId &&
                        x.Identifier.ProjectId == identifier.ProjectId && x.Identifier.AssetId == identifier.AssetId);

                    if (assetData != null)
                    {
                        var status = await GetImportStatusAsync(assetData, asset, token);
                        assetData.AssetDataAttributeCollection = new AssetDataAttributeCollection(new ImportAttribute(status));
                    }
                }
            }
            catch (ForbiddenException e)
            {
                Utilities.DevLogException(e);
            }
            finally
            {
                // Mark all datas still missing a status
                foreach (var assetData in assetDatas)
                {
                    if (assetData.AssetDataAttributeCollection?.HasAttribute<ImportAttribute>() ?? false)
                        continue;

                    assetData.AssetDataAttributeCollection =
                        new AssetDataAttributeCollection(new ImportAttribute(ImportAttribute.ImportStatus.ErrorSync));
                }
            }
        }

        public async Task<ImportAttribute.ImportStatus> GetImportStatusAsync(BaseAssetData assetData, CancellationToken token)
        {
            if (assetData == null)
            {
                return ImportAttribute.ImportStatus.NoImport;
            }

            try
            {
                var assetIdentifier = assetData.Identifier;

                var assetCacheConfiguration = new AssetCacheConfiguration
                {
                    CacheProperties = true
                };
                var cloudAsset = await GetLatestAssetVersionAsync(assetIdentifier, assetCacheConfiguration, token);

                return await GetImportStatusAsync(assetData, cloudAsset, token);
            }
            catch (ForbiddenException)
            {
                return ImportAttribute.ImportStatus.ErrorSync;
            }
            catch (HttpRequestException)
            {
                // Ignore unreachable host
                return ImportAttribute.ImportStatus.NoImport;
            }
        }

        async Task<ImportAttribute.ImportStatus> GetImportStatusAsync(BaseAssetData assetData, IAsset cloudAsset,
            CancellationToken token)
        {
            if (cloudAsset == null)
            {
                return ImportAttribute.ImportStatus.ErrorSync;
            }

            // Even if cloudAsset is != null, we might need to check if the project is archived or not.

            var cloudAssetProperties = await DataMapper.From(cloudAsset, token);

            return assetData.Identifier == cloudAssetProperties.Identifier && assetData.Updated != null &&
                assetData.Updated == cloudAssetProperties.Updated
                    ? ImportAttribute.ImportStatus.UpToDate
                    : ImportAttribute.ImportStatus.OutOfDate;
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

        async Task<AssetIdentifier> FindAssetIdentifierAsync(IAssetProject project, IAssetReference reference, CancellationToken token)
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

                var result = await FindAssetAsync(project.Descriptor.OrganizationId, filter, AssetCacheConfiguration.NoCaching, token);
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
                .LimitTo(range);

            await foreach(var reference in query.ExecuteAsync(token))
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
            return AssetRepository.EnableProjectForAssetManagerLiteAsync(projectDescriptor, token);
        }

        public async Task CreateCollectionAsync(CollectionInfo collectionInfo, CancellationToken token)
        {
            var collectionDescriptor = Map(collectionInfo);
            var project = await AssetRepository.GetAssetProjectAsync(collectionDescriptor.ProjectDescriptor, token);
            var collectionCreation = new AssetCollectionCreation(collectionInfo.Name, k_DefaultCollectionDescription)
            {
                ParentPath = collectionInfo.ParentPath
            };
            await project.CreateCollectionLiteAsync(collectionCreation, token);
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

            var projectQuery = AssetRepository.QueryAssetProjects(new OrganizationId(organizationId))
                .WithCacheConfiguration(new AssetProjectCacheConfiguration {CacheProperties = true})
                .LimitTo(range);

            await foreach (var project in projectQuery.ExecuteAsync(token))
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

        async IAsyncEnumerable<IAsset> SearchAsync(OrganizationId organizationId, IEnumerable<string> projectIds,
            AssetSearchFilter assetSearchFilter, SortField sortField, SortingOrder sortingOrder, int startIndex, int pageSize,
            AssetCacheConfiguration assetCacheConfiguration, [EnumeratorCancellation] CancellationToken token)
        {
            // Ensure that page size stays within range
            if (int.MaxValue - startIndex < pageSize)
            {
                pageSize = int.MaxValue - startIndex;
            }

            // Current issue in SDK when calculating limit requires temporary local fix:
            // var range = new Range(startIndex, pageSize > 0 ? startIndex + pageSize : Index.FromEnd(pageSize));
            var range = new Range(startIndex, pageSize > 0 ? startIndex + pageSize : Math.Max(0, int.MaxValue - startIndex - pageSize));

            var projectDescriptors = projectIds?
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(id => new ProjectDescriptor(organizationId, new ProjectId(id)))
                .ToList();

            var assetsQuery = projectDescriptors?.Count > 0
                ? AssetRepository.QueryAssets(projectDescriptors)
                : AssetRepository.QueryAssets(organizationId);
            assetsQuery.LimitTo(range)
                .WithCacheConfiguration(assetCacheConfiguration)
                .SelectWhereMatchesFilter(Map(assetSearchFilter))
                .OrderBy(sortField.ToString(), Map(sortingOrder));

            await foreach (var asset in assetsQuery.ExecuteAsync(token))
            {
                yield return asset;
            }
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

            if (assetUpdate.Metadata is { Count: > 0 })
            {
                await UpdateMetadata(assetData.Identifier, assetUpdate.Metadata, token);
            }
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
            return await DataMapper.GetPreviewFilePath(asset, token);
        }

        public async Task RemoveThumbnail(AssetData assetData, CancellationToken token)
        {
            var dataset = await GetDatasetAsync(assetData, k_PreviewDatasetTag, default, token);
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

            var dataset = await GetDatasetAsync(assetData, k_SourceDatasetTag, default, token);
            if (dataset != null)
            {
                var file = await UploadFileToDataset(dataset, destinationPath, stream, progress, token);
                if (file != null)
                {
                    // For files that are textures which are preview supported, generate tags if applicable.
                    if (AssetDataTypeHelper.IsSupportingPreviewGeneration(Path.GetExtension(destinationPath)))
                    {
                        await GenerateAndAssignTags(file, token);
                    }

                    return await DataMapper.From(file, token);
                }
            }

            return null;
        }

        public async Task RemoveAllFiles(AssetData assetData, CancellationToken token)
        {
            var asset = await InternalGetAssetAsync(assetData, token);
            if (asset == null)
            {
                return;
            }

            var cacheConfiguration = new AssetCacheConfiguration
            {
                DatasetCacheConfiguration = new DatasetCacheConfiguration
                {
                    CacheProperties = true,
                    CacheFileList = true
                }
            };
            asset = await asset.WithCacheConfigurationAsync(cacheConfiguration, token);

            await foreach (var dataset in asset.ListDatasetsAsync(Range.All, token))
            {
                var systemTags = await DataMapper.GetDatasetSystemTagsAsync(dataset, token);
                if (systemTags.Contains(k_SourceDatasetTag))
                {
                    await RemoveAllFiles(dataset, token);
                    break;
                }
            }
        }

        static async Task RemoveAllFiles(IDataset dataset, CancellationToken token)
        {
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

        public async IAsyncEnumerable<AssetDataFile> ListFilesAsync(AssetIdentifier assetIdentifier, AssetDataset assetDataset, Range range, [EnumeratorCancellation] CancellationToken token)
        {
            if (assetDataset == null)
            {
                yield break;
            }

            var cacheConfiguration = new DatasetCacheConfiguration
            {
                CacheProperties = true,
                CacheFileList = true,
                FileCacheConfiguration = new FileCacheConfiguration
                {
                    CacheProperties = true
                }
            };
            var dataset = await GetDatasetAsync(assetIdentifier, assetDataset, cacheConfiguration, token);

            if (dataset == null)
            {
                yield break;
            }

            if (!dataset.CacheConfiguration.FileCacheConfiguration.CacheProperties)
            {
                Utilities.DevLogWarning("File properties are not cached. Please ensure caching of properties for optimal AssetDataFile mapping.");
            }

            await foreach (var file in dataset.ListFilesAsync(range, token))
            {
                yield return await DataMapper.From(file, token);
            }
        }

        public async Task<AssetDataset> GetDatasetAsync(AssetData assetData, IEnumerable<string> systemTags, CancellationToken token)
        {
            var dataset = await GetDatasetAsync(assetData.Identifier, systemTags, default, token);
            return await DataMapper.From(dataset, token);
        }

        public async Task<AssetDataFile> UploadThumbnail(AssetData assetData, Texture2D thumbnail, IProgress<HttpProgress> progress, CancellationToken token)
        {
            if (assetData == null || thumbnail == null)
            {
                return null;
            }

            AssetDataFile result = null;
            byte[] bytes;
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
            var dataset = await GetDatasetAsync(assetData, k_PreviewDatasetTag, default, token);
            if (dataset != null)
            {
                var file = await UploadFileToDataset(dataset, k_ThumbnailFilename, stream, progress, token);
                if (file != null)
                {
                    await GenerateAndAssignTags(file, token);

                    result = await DataMapper.From(file, token);
                }
            }

            return result;
        }

        internal async Task GenerateAndAssignTags(IFile file, CancellationToken token)
        {
            if (!m_SettingsManager.IsTagsCreationUploadEnabled)
                return;

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
                var existingTags = await DataMapper.GetFileTagsAsync(file, token) ?? Array.Empty<string>();
                var fileUpdate = new FileUpdate
                {
                    Tags = existingTags.Union(tags).ToArray()
                };
                await file.UpdateAsync(fileUpdate, token);
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
                    UnityEngine.Debug.LogError($"Unable to upload file {destinationPath} to dataset {dataset.Descriptor.DatasetId}. Error code is {e.ErrorCode} with message \"{e.Message}\"");
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

            IDataset sourceDataset;

            if (fileCount > k_MaxNumberOfFilesForAssetDownloadUrlFetch)
            {
                // Slow path, request urls per file
                var cacheConfiguration = new DatasetCacheConfiguration
                {
                    FileCacheConfiguration = new FileCacheConfiguration
                    {
                        CacheDownloadUrl = true
                    }
                };
                sourceDataset = await GetDatasetAsync(assetData, k_SourceDatasetTag, cacheConfiguration, token);
                return await GetAssetDownloadUrlsMultipleRequestsAsync(sourceDataset, progress, token);
            }

            // Fast track, request urls in a single call
            sourceDataset = await GetDatasetAsync(assetData, k_SourceDatasetTag, default, token);
            return await GetAssetDownloadUrlsSingleRequestAsync(asset, sourceDataset, progress, token);
        }

        async Task<Dictionary<string, Uri>> GetAssetDownloadUrlsSingleRequestAsync(IAsset asset, IDataset sourceDataset, IProgress<FetchDownloadUrlsProgress> progress, CancellationToken token)
        {
            var result = new Dictionary<string, Uri>();

            if (asset == null || sourceDataset == null)
            {
                return result;
            }

            // Requesting all files url for the whole asset.
            progress?.Report(new FetchDownloadUrlsProgress("Downloading urls by asset", 0.2f));
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

            progress?.Report(new FetchDownloadUrlsProgress("Downloading urls by file", 0.0f));
            // The previous request to list all files was on the whole asset since we needed to ensure that the bulk urls fetch
            // (for the fast track) was below a threshold. Now that we are on the slow path, let's work on the "Source" dataset only
            var files = new List<IFile>(k_MaxNumberOfFilesForAssetDownloadUrlFetch * 2);
            await foreach (var file in sourceDataset.ListFilesAsync(Range.All, token))
            {
                files.Add(file);
            }

            for (var i = 0; i < files.Count; ++i)
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
                result[file.Descriptor.Path!] = url;
            }
            progress?.Report(new FetchDownloadUrlsProgress("Completed downloading urls", 1.0f));

            return result;
        }

        async Task<IDataset> GetDatasetAsync(AssetData assetData, string datasetSystemTag, DatasetCacheConfiguration cacheConfiguration, CancellationToken token)
        {
            if (assetData == null)
            {
                return null;
            }

            var assetDataset = assetData.Datasets.FirstOrDefault(d => d.SystemTags.Contains(datasetSystemTag));

            if (assetDataset != null)
            {
                return await GetDatasetAsync(assetData.Identifier, assetDataset, cacheConfiguration, token);
            }

            return await GetDatasetAsync(assetData.Identifier, new HashSet<string> { datasetSystemTag }, cacheConfiguration, token);
        }

        async Task<IDataset> GetDatasetAsync(AssetIdentifier assetIdentifier, AssetDataset assetDataset, DatasetCacheConfiguration cacheConfiguration, CancellationToken token)
        {
            if (assetDataset == null)
            {
                return null;
            }

            if (string.IsNullOrEmpty(assetDataset.Id))
            {
                return await GetDatasetAsync(assetIdentifier, assetDataset.SystemTags, cacheConfiguration, token);
            }

            var assetDescriptor = Map(assetIdentifier);
            var datasetDescriptor = new DatasetDescriptor(assetDescriptor, new DatasetId(assetDataset.Id));

            var dataset = await AssetRepository.GetDatasetAsync(datasetDescriptor, token);
            return await dataset.WithCacheConfigurationAsync(cacheConfiguration, token);
        }

        async Task<IDataset> GetDatasetAsync(AssetIdentifier assetIdentifier, IEnumerable<string> systemTags, DatasetCacheConfiguration cacheConfiguration, CancellationToken token)
        {
            var asset = await InternalGetAssetAsync(assetIdentifier, token);
            if (asset == null)
            {
                return null;
            }

            var assetCacheConfiguration = new AssetCacheConfiguration
            {
                DatasetCacheConfiguration = cacheConfiguration
            };
            asset = await asset.WithCacheConfigurationAsync(assetCacheConfiguration, token);

            try
            {
                await foreach (var dataset in asset.ListDatasetsAsync(Range.All, token))
                {
                    var datasetSystemTags = await DataMapper.GetDatasetSystemTagsAsync(dataset, token);
                    if (datasetSystemTags.Any(t => systemTags.Any(tag => tag == t)))
                    {
                        return dataset;
                    }
                }
            }
            catch (NotFoundException)
            {
                // Ignore, asset could not be found on cloud.
            }

            return null;
        }

        [Obsolete("IAsset serialization is not supported")]
        public AssetData DeserializeAssetData(string content)
        {
            var fullName = typeof(IAsset).FullName;

            // If the Cloud SDKs have been enbedded, we need to modify the namespaces
            if (!string.IsNullOrEmpty(fullName) && fullName != "Unity.Cloud.Assets.IAsset")
            {
                var updatedNamespace = fullName.Replace("IAsset", string.Empty);
                content = content.Replace("Unity.Cloud.Assets.", updatedNamespace);
            }

            var asset = AssetRepository.DeserializeAsset(content);
            return asset == null
                ? null
                : new AssetData(
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
                    asset.Tags,
                    Map(asset.Labels));
        }

        AssetCacheConfiguration GetAssetCacheConfigurationForMapping()
        {
            return new AssetCacheConfiguration
            {
                CacheProperties = true,
                CacheMetadata = true,
                CacheDatasetList = true,
                DatasetCacheConfiguration = new DatasetCacheConfiguration
                {
                    CacheProperties = true
                }
            };
        }

        IAssetRepository GetConfiguredRepository(AssetRepositoryCacheConfiguration cacheConfiguration)
        {
            return m_AssetRepositoryOverride ?? Services.GetConfiguredRepository(cacheConfiguration);
        }

        async Task<AssetData> Map(AssetDescriptor assetDescriptor, CancellationToken token)
        {
            var asset = await AssetRepository.GetAssetAsync(assetDescriptor, token);
            asset = await asset.WithCacheConfigurationAsync(GetAssetCacheConfigurationForMapping(), token);
            return await DataMapper.From(asset, token);
        }

        static IEnumerable<AssetLabel> Map(IEnumerable<LabelDescriptor> labelDescriptors)
        {
            return labelDescriptors.Select(labelDescriptor => new AssetLabel(labelDescriptor.LabelName, labelDescriptor.OrganizationId.ToString()));
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

        static string Map(FileDescriptor? fileDescriptor)
        {
            return string.IsNullOrEmpty(fileDescriptor?.Path) ? "/" : $"{fileDescriptor.Value.DatasetId}/{fileDescriptor.Value.Path}";
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

            if (assetSearchFilter.CreatedBy != null && assetSearchFilter.CreatedBy.Any())
            {
                cloudAssetSearchFilter.Include().AuthoringInfo.CreatedBy.WithValue(string.Join(" ", assetSearchFilter.CreatedBy));
            }

            if (assetSearchFilter.Status != null && assetSearchFilter.Status.Any())
            {
                cloudAssetSearchFilter.Include().Status.WithValue(string.Join(" ", assetSearchFilter.Status));
            }

            if (assetSearchFilter.UpdatedBy != null && assetSearchFilter.UpdatedBy.Any())
            {
                cloudAssetSearchFilter.Include().AuthoringInfo.UpdatedBy.WithValue(string.Join(" ", assetSearchFilter.UpdatedBy));
            }

            if (assetSearchFilter.UnityTypes != null && assetSearchFilter.UnityTypes.Any())
            {
                var regex = AssetDataTypeHelper.GetRegexForExtensions(assetSearchFilter.UnityTypes);
                cloudAssetSearchFilter.Include().Files.Path.WithValue(regex);
            }

            if (assetSearchFilter.CustomMetadata != null)
            {
                foreach (var metadataGroup in assetSearchFilter.CustomMetadata.GroupBy(m => m.FieldKey))
                {
                    var metadataList = metadataGroup.ToList();
                    if (metadataList.Any())
                    {
                        var metadata = metadataList[0];
                        if(metadata.Type == MetadataFieldType.Number)
                        {
                            cloudAssetSearchFilter.Include().Metadata.WithValue(metadataGroup.Key, Map(metadataList[0]));
                        }
                        else if(metadata.Type == MetadataFieldType.Timestamp)
                        {
                            var minValue = metadataList.Min(m => ((TimestampMetadata)m).Value.DateTime);
                            var maxValue = metadataList.Max(m => ((TimestampMetadata)m).Value.DateTime);
                            cloudAssetSearchFilter.Include().Metadata.WithTimestampValue(metadataGroup.Key, minValue, true, maxValue);
                        }
                        else if (metadata.Type == MetadataFieldType.Text)
                        {
                            var stringOp = new StringPredicate( ((TextMetadata)metadata).Value, StringSearchOption.Prefix);
                            cloudAssetSearchFilter.Include().Metadata.WithTextValue(metadataGroup.Key, stringOp);
                        }
                        else if (metadata.Type == MetadataFieldType.Url)
                        {
                            var stringOp = new StringPredicate( $"[{((UrlMetadata)metadata).Value.Label}]", StringSearchOption.Prefix);
                            cloudAssetSearchFilter.Include().Metadata.WithTextValue(metadataGroup.Key, stringOp);
                        }
                        else
                        {
                            var metadataValue = Map(metadata);
                            cloudAssetSearchFilter.Include().Metadata.WithValue(metadata.FieldKey, metadataValue);
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(assetSearchFilter.Collection))
            {
                cloudAssetSearchFilter.Collections.WhereContains(new CollectionPath(assetSearchFilter.Collection));
            }

            if (assetSearchFilter.Searches is {Count: > 0})
            {
                var searchString = string.Concat("*", string.Join('*', assetSearchFilter.Searches), "*");
                cloudAssetSearchFilter.Any().Name.WithValue(searchString);
                cloudAssetSearchFilter.Any().Description.WithValue(searchString);
                cloudAssetSearchFilter.Any().Tags.WithValue(searchString);
            }

            if (assetSearchFilter.AssetIds is {Count: > 0})
            {
                var searchString = string.Join(' ', assetSearchFilter.AssetIds);
                cloudAssetSearchFilter.Any().Id.WithValue(searchString);
            }

            if (assetSearchFilter.AssetVersions is {Count: > 0})
            {
                var searchString = string.Join(' ', assetSearchFilter.AssetVersions.Select(OptimizeVersionForSearch));
                cloudAssetSearchFilter.Any().Version.WithValue(searchString);
            }

            return cloudAssetSearchFilter;
        }

        static string OptimizeVersionForSearch(string version)
        {
            // Because of how elastic search tokenizes strings, we need to manipulate the version to minimize false positive results
            // We will therefore keep only that last component of the version string
            return version.Split('-')[^1];
        }

        static GroupableField Map(AssetSearchGroupBy groupBy)
        {
            return groupBy switch
            {
                AssetSearchGroupBy.Name => GroupableField.Name,
                AssetSearchGroupBy.Status => GroupableField.Status,
                AssetSearchGroupBy.CreatedBy => GroupableField.CreatedBy,
                AssetSearchGroupBy.UpdatedBy => GroupableField.UpdateBy,
                _ => throw new ArgumentOutOfRangeException(nameof(groupBy), groupBy, null)
            };
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
                Tags = assetCreation.Tags,
                Metadata = Map(assetCreation.Metadata)
            };
        }

        static Dictionary<string, MetadataValue> Map(List<IMetadata> metadataList)
        {
            var metadataDictionary = new Dictionary<string, MetadataValue>();
            foreach (var metadata in metadataList)
            {
                metadataDictionary.Add(metadata.FieldKey, Map(metadata));
            }

            return metadataDictionary;
        }

        static MetadataValue Map(IMetadata metadata) => metadata.Type switch
        {
            MetadataFieldType.Boolean => new Cloud.AssetsEmbedded.BooleanMetadata(((BooleanMetadata)metadata).Value),
            MetadataFieldType.Text => new StringMetadata(((TextMetadata)metadata).Value),
            MetadataFieldType.Number => new Cloud.AssetsEmbedded.NumberMetadata(((NumberMetadata)metadata).Value),
            MetadataFieldType.Url => new Cloud.AssetsEmbedded.UrlMetadata(((UrlMetadata)metadata).Value.Uri, ((UrlMetadata)metadata).Value.Label),
            MetadataFieldType.Timestamp => new DateTimeMetadata(((TimestampMetadata)metadata).Value.DateTime),
            MetadataFieldType.User => new Cloud.AssetsEmbedded.UserMetadata(new UserId(((UserMetadata)metadata).Value)),
            MetadataFieldType.SingleSelection => new Cloud.AssetsEmbedded.SingleSelectionMetadata(((SingleSelectionMetadata)metadata).Value),
            MetadataFieldType.MultiSelection => new Cloud.AssetsEmbedded.MultiSelectionMetadata(((MultiSelectionMetadata)metadata).Value.ToArray()),
            _ => throw new InvalidOperationException("Unexpected metadata field type was encountered.")
        };

        static IAssetUpdate Map(AssetUpdate assetUpdate)
        {
            if (assetUpdate == null)
            {
                return null;
            }

            return new Unity.Cloud.AssetsEmbedded.AssetUpdate
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

        /// <summary>
        /// Wraps the mapping of certain value types to make them compatible with mocking for tests.
        /// </summary>
        internal interface IDataMapper
        {
            async Task<string> GetPreviewFilePath(IAsset asset, CancellationToken token)
            {
                if (asset == null)
                {
                    return null;
                }

                var properties = await asset.GetPropertiesAsync(token);
                return Map(properties.PreviewFileDescriptor);
            }

            async Task<IEnumerable<string>> GetDatasetSystemTagsAsync(IDataset dataset, CancellationToken token)
            {
                if (dataset == null)
                {
                    return Array.Empty<string>();
                }

                var datasetProperties = await dataset.GetPropertiesAsync(token);
                return datasetProperties.SystemTags ?? Array.Empty<string>();
            }

            async Task<IEnumerable<string>> GetFileTagsAsync(IFile file, CancellationToken token)
            {
                if (file == null)
                {
                    return Array.Empty<string>();
                }

                var fileProperties = await file.GetPropertiesAsync(token);
                return fileProperties.Tags ?? Array.Empty<string>();
            }

            async Task<(ProjectInfo projectInfo, bool hasCollection)> From(IAssetProject project, CancellationToken token)
            {
                var projectProperties = await project.GetPropertiesAsync(token);
                return (new ProjectInfo
                {
                    Id = project.Descriptor.ProjectId.ToString(),
                    Name = projectProperties.Name,
                }, projectProperties.HasCollection);
            }

            async Task<AssetData> From(IAsset asset, CancellationToken token)
            {
                if (asset == null)
                {
                    return null;
                }

                if (!asset.CacheConfiguration.CacheProperties)
                {
                    Utilities.DevLogWarning("Asset properties are not cached. Please ensure caching of properties for optimal AssetData mapping.");
                }

                var properties = await asset.GetPropertiesAsync(token);
                var data = From(asset.Descriptor, properties);

                if (data == null)
                {
                    return null;
                }

                if (asset.CacheConfiguration.CacheMetadata)
                {
                    var metadata = await GetMetadataAsync(asset.Metadata as IReadOnlyMetadataContainer, token);
                    data.SetMetadata(metadata);
                }

                if (asset.CacheConfiguration.CacheDatasetList)
                {
                    var datasets = new List<AssetDataset>();
                    await foreach (var dataset in asset.ListDatasetsAsync(Range.All, token))
                    {
                        var assetDataset = await From(dataset, token);
                        datasets.Add(assetDataset);
                    }

                    data.Datasets = datasets;
                }

                return data;
            }

            static MetadataFieldType GetCorrectSelectionFieldType(SelectionFieldDefinitionProperties properties) =>
                properties.Multiselection ? MetadataFieldType.MultiSelection : MetadataFieldType.SingleSelection;

            static AssetData From(AssetDescriptor descriptor, AssetProperties properties)
            {
                return new AssetData(
                    Map(descriptor),
                    properties.FrozenSequenceNumber,
                    properties.ParentFrozenSequenceNumber,
                    properties.Changelog,
                    properties.Name,
                    Map(properties.Type),
                    properties.StatusName,
                    properties.Description,
                    properties.AuthoringInfo.Created,
                    properties.AuthoringInfo.Updated,
                    properties.AuthoringInfo.CreatedBy.ToString(),
                    properties.AuthoringInfo.UpdatedBy.ToString(),
                    Map(properties.PreviewFileDescriptor),
                    properties.State == AssetState.Frozen,
                    properties.Tags,
                    Map(properties.Labels));
            }

            async Task<AssetDataset> From(IDataset dataset, CancellationToken token)
            {
                if (dataset == null)
                {
                    return null;
                }

                if (!dataset.CacheConfiguration.CacheProperties)
                {
                    Utilities.DevLogWarning("Dataset properties are not cached. Please ensure caching of properties for optimal AssetDataset mapping.");
                }

                var properties = await dataset.GetPropertiesAsync(token);
                return new AssetDataset(
                    dataset.Descriptor.DatasetId.ToString(),
                    properties.Name,
                    properties.SystemTags);
            }

            async Task<AssetDataFile> From(IFile file, CancellationToken token)
            {
                if (file == null)
                {
                    return null;
                }

                var properties = await file.GetPropertiesAsync(token);
                return From(file.Descriptor.Path, properties);
            }

            static AssetDataFile From(string filePath, FileProperties fileProperties)
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    return null;
                }

                var available = string.IsNullOrEmpty(fileProperties.StatusName) ||
                    fileProperties.StatusName.Equals("Uploaded", StringComparison.OrdinalIgnoreCase);

                return new AssetDataFile(
                    filePath,
                    Path.GetExtension(filePath).ToLower(),
                    null,
                    fileProperties.Description,
                    fileProperties.Tags,
                    fileProperties.SizeBytes,
                    available);
            }

            async Task<IMetadataFieldDefinition> From(IFieldDefinition fieldDefinition, CancellationToken token)
            {
                var properties = await fieldDefinition.GetPropertiesAsync(token);

                var fieldType = properties.Type switch
                {
                    FieldDefinitionType.Boolean => MetadataFieldType.Boolean,
                    FieldDefinitionType.Text => MetadataFieldType.Text,
                    FieldDefinitionType.Number => MetadataFieldType.Number,
                    FieldDefinitionType.Url => MetadataFieldType.Url,
                    FieldDefinitionType.Timestamp => MetadataFieldType.Timestamp,
                    FieldDefinitionType.User => MetadataFieldType.User,
                    FieldDefinitionType.Selection => GetCorrectSelectionFieldType(properties.AsSelectionFieldDefinitionProperties()),
                    _ => throw new InvalidOperationException("Unexpected field definition type was encountered.")
                };

                if (properties.Type == FieldDefinitionType.Selection)
                {
                    var selectionProperties = properties.AsSelectionFieldDefinitionProperties();
                    return new SelectionFieldDefinition(fieldDefinition.Descriptor.FieldKey, properties.DisplayName, fieldType, selectionProperties.AcceptedValues);
                }

                return new MetadataFieldDefinition(fieldDefinition.Descriptor.FieldKey, properties.DisplayName, fieldType);
            }

            static async Task<List<IMetadata>> GetMetadataAsync(IReadOnlyMetadataContainer metadataContainer, CancellationToken token = default)
            {
                if (metadataContainer == null)
                {
                    return null;
                }

                var projectOrganizationProvider = ServicesContainer.instance.Resolve<IProjectOrganizationProvider>();
                var fieldDefinitions = projectOrganizationProvider.SelectedOrganization.MetadataFieldDefinitions;

                var metadataQuery = metadataContainer.Query();
                var metadataFields = new List<IMetadata>();
                await foreach (var result in metadataQuery.ExecuteAsync(token))
                {
                    var def = fieldDefinitions.Find(x => x.Key == result.Key);

                    if (def == null)
                        continue;

                    IMetadata value = def.Type switch
                    {
                        MetadataFieldType.Text => new TextMetadata(def.Key, def.DisplayName, result.Value.AsText().Value),
                        MetadataFieldType.Boolean => new BooleanMetadata(def.Key, def.DisplayName, result.Value.AsBoolean().Value),
                        MetadataFieldType.Number => new NumberMetadata(def.Key, def.DisplayName, result.Value.AsNumber().Value),
                        MetadataFieldType.Url => new UrlMetadata(def.Key, def.DisplayName, new UriEntry(result.Value.AsUrl().Uri, result.Value.AsUrl().Label)),
                        MetadataFieldType.Timestamp => new TimestampMetadata(def.Key, def.DisplayName, new DateTimeEntry(result.Value.AsTimestamp().Value)),
                        MetadataFieldType.User => new UserMetadata(def.Key, def.DisplayName, result.Value.AsUser().UserId.ToString()),
                        MetadataFieldType.SingleSelection => new SingleSelectionMetadata(def.Key, def.DisplayName, result.Value.AsSingleSelection().SelectedValue),
                        MetadataFieldType.MultiSelection => new MultiSelectionMetadata(def.Key, def.DisplayName, result.Value.AsMultiSelection().SelectedValues),
                        _ => null
                    };

                    if (value != null)
                        metadataFields.Add(value);
                }

                return metadataFields;
            }
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

            public static IAssetRepository GetConfiguredRepository(AssetRepositoryCacheConfiguration cacheConfiguration)
            {
                if (s_AssetRepository == null)
                {
                    CreateServices();
                }

                return AssetRepositoryFactory.Create(s_ServiceHttpClient, s_ServiceHostResolver, cacheConfiguration);
            }

            static void CreateServices()
            {
                var pkgInfo = PackageInfo.FindForAssembly(Assembly.GetAssembly(typeof(Services)));
                var httpClient = new UnityHttpClient();
                s_ServiceHostResolver = UnityRuntimeServiceHostResolverFactory.Create();

                UnityEditorServiceAuthorizer.instance.AuthenticationStateChanged += OnAuthenticationStateChanged;

                s_ServiceHttpClient = new ServiceHttpClient(httpClient, UnityEditorServiceAuthorizer.instance, new AppIdProvider())
                    .WithApiSourceHeaders(pkgInfo.name, pkgInfo.version);

                s_AssetRepository = AssetRepositoryFactory.Create(s_ServiceHttpClient, s_ServiceHostResolver, AssetRepositoryCacheConfiguration.NoCaching);
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
