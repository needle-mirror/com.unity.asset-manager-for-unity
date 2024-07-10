using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;
using Unity.Cloud.Common.Runtime;
using Unity.Cloud.Identity;
using Unity.Cloud.Identity.Editor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Unity.AssetManager.Editor
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

    interface IAssetsProvider : IService
    {
        event Action<AuthenticationState> AuthenticationStateChanged;
        AuthenticationState AuthenticationState { get; }

        Task<OrganizationInfo> GetOrganizationInfoAsync(string organizationId, CancellationToken token);
        IAsyncEnumerable<IOrganization> ListOrganizationsAsync(Range range, CancellationToken token);

        IAsyncEnumerable<AssetData> SearchAsync(string organizationId, IEnumerable<string> projectIds,
            IAssetSearchFilter assetSearchFilter, int startIndex, int pageSize, CancellationToken token);

        Task<Dictionary<string, string>> GetProjectIconUrlsAsync(string organizationId, CancellationToken token);

        Task<List<string>> GetFilterSelectionsAsync(string organizationId, IEnumerable<string> projectIds,
            IAssetSearchFilter searchFilter,
            GroupableField groupBy, CancellationToken token);

        Task<AssetData> CreateAssetAsync(ProjectDescriptor projectDescriptor, IAssetCreation assetCreation, CancellationToken token);

        Task<AssetData> CreateUnfrozenVersionAsync(AssetData assetData, CancellationToken token);

        Task<AssetData> GetAssetAsync(AssetDescriptor assetDescriptor, CancellationToken token);

        Task<AssetData> GetLatestAssetVersionAsync(ProjectDescriptor projectDescriptor, AssetId assetId, CancellationToken token);

        Task<AssetComparisonResult> CompareAssetWithCloudAsync(AssetData assetData, CancellationToken token);

        Task<ICloudStorageUsage> GetOrganizationCloudStorageUsageAsync(IOrganization organization, CancellationToken token = default);

        Task EnableProjectAsync(CancellationToken token = default);

        IAsyncEnumerable<IMemberInfo> GetOrganizationMembersAsync(string organizationId, Range range,
            CancellationToken token);

        Task<IOrganization> GetOrganizationAsync(string organizationId);

        Task<IDataset> GetPreviewDatasetAsync(AssetData assetData, CancellationToken token);
        Task<IDataset> GetSourceDatasetAsync(AssetData assetData, CancellationToken token);

        Task UpdateAsync(AssetData assetData, IAssetUpdate assetUpdate, CancellationToken token);
        Task UpdateStatusAsync(AssetData assetData, AssetStatusAction statusAction, CancellationToken token);
        Task FreezeAsync(AssetData assetData, string changeLog, CancellationToken token);
        Task RefreshAsync(AssetData assetData, CancellationToken token);

        Task<IDictionary<string, Uri>> GetAssetDownloadUrlsAsync(AssetData assetData, CancellationToken token);

        IAsyncEnumerable<IFile> ListFilesAsync(AssetData assetData, Range range, CancellationToken token);

        void OnAfterDeserializeAssetData(AssetData assetData);
    }

    [Serializable]
    class AssetsSdkProvider : BaseService<IAssetsProvider>, IAssetsProvider
    {
        [SerializeReference]
        IUnityConnectProxy m_UnityConnectProxy;

        IAssetRepository AssetRepository => Services.AssetRepository;

        [ServiceInjection]
        public void Inject(IUnityConnectProxy unityConnectProxy)
        {
            m_UnityConnectProxy = unityConnectProxy;
        }

        public event Action<AuthenticationState> AuthenticationStateChanged;

        public AuthenticationState AuthenticationState => Map(Services.AuthenticationState);

        AuthenticationState Map(Unity.Cloud.Identity.AuthenticationState authenticationState) =>
            authenticationState switch
            {
                Unity.Cloud.Identity.AuthenticationState.AwaitingInitialization => AuthenticationState.AwaitingInitialization,
                Unity.Cloud.Identity.AuthenticationState.AwaitingLogin => AuthenticationState.AwaitingLogin,
                Unity.Cloud.Identity.AuthenticationState.LoggedIn => AuthenticationState.LoggedIn,
                Unity.Cloud.Identity.AuthenticationState.AwaitingLogout => AuthenticationState.AwaitingLogout,
                Unity.Cloud.Identity.AuthenticationState.LoggedOut => AuthenticationState.LoggedOut,
                _ => throw new ArgumentOutOfRangeException(nameof(authenticationState), authenticationState, null)
            };

        public override void OnEnable()
        {
            Services.AuthenticationStateChanged += OnAuthenticationStateChanged;
            Services.InitAuthenticatedServices();
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

            var organizationInfo = new OrganizationInfo {Id = organizationId};

            if (organizationId == "none")
            {
                return organizationInfo;
            }

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

            return organizationInfo;
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
            IAssetSearchFilter assetSearchFilter, int startIndex, int pageSize,
            [EnumeratorCancellation] CancellationToken token)
        {
            var range = new Range(startIndex, startIndex + pageSize);

            Utilities.DevLog($"Fetching {range} Assets ...");

            var t = new Stopwatch();
            t.Start();

            var count = 0;
            await foreach (var asset in SearchAsync(organizationId, projectIds, assetSearchFilter, range, token))
            {
                yield return asset;
                ++count;
            }

            t.Stop();

            Utilities.DevLog($"Fetched {count} Assets from {range} in {t.ElapsedMilliseconds}ms");
        }

        public async Task<ICloudStorageUsage> GetOrganizationCloudStorageUsageAsync(IOrganization organization, CancellationToken token = default)
        {
            return await organization.GetCloudStorageUsageAsync(token);
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
            IAssetSearchFilter searchFilter, GroupableField groupBy, CancellationToken token)
        {
            var strongTypedOrgId = new OrganizationId(organizationId);
            var projectDescriptors = projectIds.Select(p => new ProjectDescriptor(strongTypedOrgId, new ProjectId(p))).ToList();

            var groupAndCountAssetsQueryBuilder = AssetRepository.GroupAndCountAssets(projectDescriptors)
                .SelectWhereMatchesFilter(searchFilter);
            var aggregation = await groupAndCountAssetsQueryBuilder.ExecuteAsync(groupBy, token);

            var result = aggregation.Keys.Distinct().ToList();
            result.Sort();

            return result;
        }

        public async Task<AssetData> CreateAssetAsync(ProjectDescriptor projectDescriptor, IAssetCreation assetCreation, CancellationToken token)
        {
            var project = await AssetRepository.GetAssetProjectAsync(projectDescriptor, token);
            var asset = await project.CreateAssetAsync(assetCreation, token);
            return asset == null ? null : new AssetData(asset);
        }

        public async Task<AssetData> CreateUnfrozenVersionAsync(AssetData assetData, CancellationToken token)
        {
            if (assetData != null && assetData.Asset != null)
            {
                var unfrozenVersion = await assetData.Asset.CreateUnfrozenVersionAsync(token);
                return unfrozenVersion == null ? null : new AssetData(unfrozenVersion);
            }

            return null;
        }

        public async Task<AssetData> GetAssetAsync(AssetDescriptor assetDescriptor, CancellationToken token)
        {
            var asset = await AssetRepository.GetAssetAsync(assetDescriptor, token);
            return asset == null ? null : new AssetData(asset);
        }

        public async Task<AssetData> GetLatestAssetVersionAsync(ProjectDescriptor projectDescriptor, AssetId assetId, CancellationToken token)
        {
            var project = await AssetRepository.GetAssetProjectAsync(projectDescriptor, token);
            var asset = await project.GetAssetWithLatestVersionAsync(assetId, token);
            return new AssetData(asset);
        }

        public async Task<AssetComparisonResult> CompareAssetWithCloudAsync(AssetData assetData, CancellationToken token)
        {
            if (assetData == null)
            {
                return AssetComparisonResult.Unknown;
            }

            try
            {
                var assetIdentifier = assetData.Identifier;
                var cloudAsset = await GetLatestAssetVersionAsync(assetIdentifier.ToAssetDescriptor().ProjectDescriptor,
                    assetIdentifier.ToAssetDescriptor().AssetId,
                    token);

                if (cloudAsset == null)
                {
                    return AssetComparisonResult.NotFoundOrInaccessible;
                }

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

        public Task EnableProjectAsync(CancellationToken token = default)
        {
            return Services.ProjectEnabler.EnableProjectAsync(m_UnityConnectProxy.ProjectId, token);
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
            while (Services.AuthenticationState != Unity.Cloud.Identity.AuthenticationState.LoggedIn)
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
            IAssetSearchFilter assetSearchFilter, Range range, [EnumeratorCancellation] CancellationToken token)
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
                .SelectWhereMatchesFilter(assetSearchFilter);

            await foreach (var asset in assetQueryBuilder.ExecuteAsync(token))
            {
                yield return asset == null ? null : new AssetData(asset);
            }
        }

        public Task<IDataset> GetPreviewDatasetAsync(AssetData assetData, CancellationToken token)
        {
            const string previewTag = "Preview";
            return GetDatasetAsync(assetData, previewTag, token);
        }

        public Task<IDataset> GetSourceDatasetAsync(AssetData assetData, CancellationToken token)
        {
            const string sourceTag = "Source";
            return GetDatasetAsync(assetData, sourceTag, token);
        }

        async Task<IDataset> GetDatasetAsync(AssetData assetData, string systemTag, CancellationToken token)
        {
            if (assetData?.Asset == null)
            {
                return null;
            }

            await foreach (var dataset in assetData.Asset.ListDatasetsAsync(Range.All, token))
            {
                if (dataset.SystemTags != null && dataset.SystemTags.Contains(systemTag))
                {
                    return dataset;
                }
            }

            return null;
        }

        public Task UpdateAsync(AssetData assetData, IAssetUpdate assetUpdate, CancellationToken token)
        {
            return assetData?.Asset == null ? Task.CompletedTask : assetData.Asset.UpdateAsync(assetUpdate, token);
        }

        public Task UpdateStatusAsync(AssetData assetData, AssetStatusAction statusAction, CancellationToken token)
        {
            return assetData?.Asset == null ? Task.CompletedTask : assetData.Asset.UpdateStatusAsync(statusAction, token);
        }

        public Task FreezeAsync(AssetData assetData, string changeLog, CancellationToken token)
        {
            return assetData?.Asset == null ? Task.CompletedTask : assetData.Asset.FreezeAsync(changeLog, token);
        }

        public Task RefreshAsync(AssetData assetData, CancellationToken token)
        {
            return assetData?.Asset == null ? Task.CompletedTask : assetData.Asset.RefreshAsync(token);
        }

        public async IAsyncEnumerable<IFile> ListFilesAsync(AssetData assetData, Range range, [EnumeratorCancellation] CancellationToken token)
        {
            if (assetData?.Asset == null)
            {
                yield break;
            }

            await foreach (var file in assetData.Asset.ListFilesAsync(range, token))
            {
                yield return file;
            }
        }

        public Task<IDictionary<string, Uri>> GetAssetDownloadUrlsAsync(AssetData assetData, CancellationToken token)
        {
            if (assetData == null || assetData.Asset == null)
            {
                return Task.FromResult<IDictionary<string, Uri>>(null);
            }

            return assetData.Asset.GetAssetDownloadUrlsAsync(token);
        }

        public void OnAfterDeserializeAssetData(AssetData assetData)
        {
            assetData.Asset = AssetRepository.DeserializeAsset(assetData.AssetSerialized);
        }

        static class Services
        {
            static IAssetRepository s_AssetRepository;
            static ProjectEnabler s_ProjectEnabler;
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

            public static ProjectEnabler ProjectEnabler
            {
                get
                {
                    if (s_ProjectEnabler == null)
                    {
                        CreateServices();
                    }

                    return s_ProjectEnabler;
                }
            }

            public static Unity.Cloud.Identity.AuthenticationState AuthenticationState =>
                UnityEditorServiceAuthorizer.instance.AuthenticationState;

            public static Action AuthenticationStateChanged;

            internal static void InitAuthenticatedServices()
            {
                if (s_AssetRepository == null || s_ProjectEnabler == null)
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

                s_ProjectEnabler = new ProjectEnabler(s_ServiceHttpClient, s_ServiceHostResolver);
            }

            static void OnAuthenticationStateChanged(Unity.Cloud.Identity.AuthenticationState state)
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
