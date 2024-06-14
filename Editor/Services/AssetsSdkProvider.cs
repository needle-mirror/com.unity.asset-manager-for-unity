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
using Debug = UnityEngine.Debug;
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

    interface IAssetsProvider : IService
    {
        Task<OrganizationInfo> GetOrganizationInfoAsync(string organizationId, CancellationToken token);

        IAsyncEnumerable<IAsset> SearchAsync(string organizationId, IEnumerable<string> projectIds,
            IAssetSearchFilter assetSearchFilter, int startIndex, int pageSize, CancellationToken token);

        Task<Dictionary<string, string>> GetProjectIconUrlsAsync(string organizationId, CancellationToken token);

        Task<List<string>> GetFilterSelectionsAsync(string organizationId, IEnumerable<string> projectIds,
            IAssetSearchFilter searchFilter,
            GroupableField groupBy, CancellationToken token);

        Task<IAsset> CreateAssetAsync(ProjectDescriptor projectDescriptor, IAssetCreation assetCreation, CancellationToken token);

        Task<IAsset> GetAssetAsync(AssetDescriptor assetDescriptor, CancellationToken token);

        Task<IAsset> GetLatestAssetVersionAsync(ProjectDescriptor projectDescriptor, AssetId assetId, CancellationToken token);

        Task<AssetComparisonResult> CompareAssetWithCloudAsync(IAsset asset, CancellationToken token);

        Task<ICloudStorageUsage> GetOrganizationCloudStorageUsageAsync(IOrganization organization, CancellationToken token = default);

        Task EnableProjectAsync(CancellationToken token = default);

        IAsyncEnumerable<IMemberInfo> GetOrganizationMembersAsync(string organizationId, Range range,
            CancellationToken token);

        Task<IOrganization> GetOrganizationAsync(string organizationId);
    }

    [Serializable]
    class AssetsSdkProvider : BaseService<IAssetsProvider>, IAssetsProvider
    {
        [SerializeReference]
        IUnityConnectProxy m_UnityConnectProxy;

        static readonly int k_ProjectPageSize = 25;

        IAssetRepository AssetRepository => Services.AssetRepository;

        [ServiceInjection]
        public void Inject(IUnityConnectProxy unityConnectProxy)
        {
            m_UnityConnectProxy = unityConnectProxy;
        }

        public async Task<OrganizationInfo> GetOrganizationInfoAsync(string organizationId, CancellationToken token)
        {
            var t = new Stopwatch();
            t.Start();

#if UNITY_2021
            var orgFromOrgName = await GetOrganizationFromOrganizationName(organizationId);
            organizationId = orgFromOrgName?.Id.ToString();
#endif

            var tasks = new List<Task>();
            var organizationInfo = new OrganizationInfo { Id = organizationId };

            var begin = -k_ProjectPageSize;
            var end = 0;
            var count = 0;

            while (count == end)
            {
                begin += k_ProjectPageSize;
                end += k_ProjectPageSize;

                await foreach (var project in GetCurrentUserProjectList(organizationId, new Range(begin, end), token))
                {
                    if (project == null)
                        continue;

                    tasks.Add(GetCollectionsAsync(project, token, infos =>
                    {
                        organizationInfo.ProjectInfos.Add(new ProjectInfo
                        {
                            Id = project.Descriptor.ProjectId.ToString(),
                            Name = project.Name,
                            CollectionInfos = infos.ToList()
                        });
                    }));

                    ++count;
                }
            }

            await Task.WhenAll(tasks);

            t.Stop();

            Utilities.DevLog($"Fetching {count} Projects took {t.ElapsedMilliseconds}ms");

            return organizationInfo;
        }

        public async IAsyncEnumerable<IAsset> SearchAsync(string organizationId, IEnumerable<string> projectIds,
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

        public async Task<IAsset> CreateAssetAsync(ProjectDescriptor projectDescriptor, IAssetCreation assetCreation, CancellationToken token)
        {
            var project = await AssetRepository.GetAssetProjectAsync(projectDescriptor, token);
            return await project.CreateAssetAsync(assetCreation, token);
        }

        public async Task<IAsset> GetAssetAsync(AssetDescriptor assetDescriptor, CancellationToken token)
        {
            return await AssetRepository.GetAssetAsync(assetDescriptor, token);
        }

        public async Task<IAsset> GetLatestAssetVersionAsync(ProjectDescriptor projectDescriptor, AssetId assetId, CancellationToken token)
        {
            var project = await AssetRepository.GetAssetProjectAsync(projectDescriptor, token);
            return await project.QueryAssetVersions(assetId).SearchLatestAssetVersionAsync(token);
        }

        public async Task<AssetComparisonResult> CompareAssetWithCloudAsync(IAsset asset, CancellationToken token)
        {
            if (asset == null)
            {
                return AssetComparisonResult.Unknown;
            }

            try
            {
                var assetDescriptor = asset.Descriptor;
                var cloudAsset = await GetLatestAssetVersionAsync(assetDescriptor.ProjectDescriptor, assetDescriptor.AssetId, token);

                if (cloudAsset == null)
                {
                    return AssetComparisonResult.NotFoundOrInaccessible;
                }

                return assetDescriptor == cloudAsset.Descriptor && asset.AuthoringInfo != null && asset.AuthoringInfo.Updated == cloudAsset.AuthoringInfo?.Updated
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

        public async Task GetCollectionsAsync(IAssetProject project, CancellationToken token,
            Action<IEnumerable<CollectionInfo>> completedCallback)
        {
            var assetCollections = project.ListCollectionsAsync(Range.All, token);
            var collections = new List<CollectionInfo>();
            await foreach (var assetCollection in assetCollections)
            {
                collections.Add(new CollectionInfo
                {
                    OrganizationId = project.Descriptor.OrganizationId.ToString(),
                    ProjectId = project.Descriptor.ProjectId.ToString(),
                    Name = assetCollection.Name,
                    ParentPath = assetCollection.ParentPath
                });
            }

            completedCallback?.Invoke(collections);
        }

        async IAsyncEnumerable<IAssetProject> GetCurrentUserProjectList(string organizationId, Range range,
            [EnumeratorCancellation] CancellationToken token)
        {
            // First call to backend requires a valid authentication state
            while (!Services.AuthenticationState.Equals(AuthenticationState.LoggedIn))
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

        async IAsyncEnumerable<IAsset> SearchAsync(string organizationId, IEnumerable<string> projectIds,
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
                yield return asset;
            }
        }
    }

    public static class AssetsSdkUtilities
    {
        public static async Task<IAsset> SearchLatestAssetVersionAsync(this AssetVersionQueryBuilder queryBuilder, CancellationToken cancellationToken)
        {
            var results = queryBuilder
                .OrderBy("versionNumber", SortingOrder.Descending)
                .LimitTo(..1)
                .ExecuteAsync(cancellationToken);

            var assets = new List<IAsset>();
            await foreach (var asset in results.WithCancellation(cancellationToken))
            {
                assets.Add(asset);
            }

            return assets.FirstOrDefault();
        }
    }

    public static class Services
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

        public static IOrganizationRepository OrganizationRepository => UnityEditorCloudServiceAuthorizer.instance;

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

        public static AuthenticationState AuthenticationState =>
            UnityEditorCloudServiceAuthorizer.instance.AuthenticationState;

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

            UnityEditorCloudServiceAuthorizer.instance.AuthenticationStateChanged += OnAuthenticationStateChanged;

            s_ServiceHttpClient = new ServiceHttpClient(httpClient, UnityEditorCloudServiceAuthorizer.instance, new AppIdProvider())
                .WithApiSourceHeaders(pkgInfo.name, pkgInfo.version);

            s_AssetRepository = AssetRepositoryFactory.Create(s_ServiceHttpClient, s_ServiceHostResolver);

            s_ProjectEnabler = new ProjectEnabler(s_ServiceHttpClient, s_ServiceHostResolver);
        }

        static void OnAuthenticationStateChanged(AuthenticationState state)
        {
            AuthenticationStateChanged?.Invoke();
        }

        // Awaiting userInfo result to contain UserEntitlements, we fetch directly the response from Genesis
        internal static async Task<IEnumerable<UserEntitlement>> GetUserEntitlementsAsync(CancellationToken token)
        {
            var userInfo = await UnityEditorCloudServiceAuthorizer.instance.GetUserInfoAsync();

            if (token.IsCancellationRequested)
            {
                return null;
            }

            var unityServicesDomainResolver = new UnityServicesDomainResolver(true);
            var internalServiceHostResolver = s_ServiceHostResolver.CreateCopyWithDomainResolverOverride(unityServicesDomainResolver);
            var url = internalServiceHostResolver.GetResolvedRequestUri($"/api/commerce/genesis/v1/entitlements?namespace=unity_editor&type=EDITOR&ownerType=USER&ownerId={userInfo.UserId}&isActive=true");

            var response = await s_ServiceHttpClient.GetAsync(url, cancellationToken: token);
            var userEntitlementsJson = await response.JsonDeserializeAsync<UserEntitlements>();
            return userEntitlementsJson.Results;
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
