using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
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

    internal interface IAssetsProvider : IService
    {
        Task<OrganizationInfo> GetOrganizationInfoAsync(string organizationId, CancellationToken token);
        IAsyncEnumerable<IAsset> SearchAsync(string organizationId, IEnumerable<string> projectIds, IAssetSearchFilter assetSearchFilter, int startIndex, int pageSize, CancellationToken token);
        Task<Dictionary<string, string>> GetProjectIconUrlsAsync(string organizationId, CancellationToken token);
        Task<List<string>> GetFilterSelectionsAsync(string organizationId, IEnumerable<string> projectIds, IAssetSearchFilter searchFilter,
            GroupableField groupBy, CancellationToken token);
        Task<AssetComparisonResult> CompareAssetWithCloudAsync(IAssetData assetData, CancellationToken token);
        Task EnableProjectAsync(CancellationToken token = default);
    }

    [Serializable]
    class AssetsSdkProvider : BaseService<IAssetsProvider>, IAssetsProvider
    {
        CloudAssetsProxy m_CloudAssetsProxy;

        [SerializeReference]
        IUnityConnectProxy m_UnityConnectProxy;

        [ServiceInjection]
        public void Inject(IUnityConnectProxy unityConnectProxy)
        {
            m_UnityConnectProxy = unityConnectProxy;
        }

        public override void OnEnable()
        {
            m_CloudAssetsProxy = new CloudAssetsProxy(m_UnityConnectProxy);
        }

        public async Task<OrganizationInfo> GetOrganizationInfoAsync(string organizationId, CancellationToken token)
        {
            var t = new Stopwatch();
            t.Start();

            var tasks = new List<Task>();
            var organizationInfo = new OrganizationInfo { id = organizationId };

            var count = 0;
            await foreach (var project in m_CloudAssetsProxy.GetCurrentUserProjectList(organizationId, Range.All, token))
            {
                if (project == null)
                    continue;

                tasks.Add(GetCollectionsAsync(organizationId, project, token, infos =>
                {
                    organizationInfo.projectInfos.Add(new ProjectInfo()
                    {
                        id = project.Descriptor.ProjectId.ToString(),
                        name = project.Name,
                        collectionInfos = infos.ToList()
                    });
                }));

                ++count;
            }

            await Task.WhenAll(tasks);

            t.Stop();

            Utilities.DevLog($"Fetching {count} Projects took {t.ElapsedMilliseconds}ms");

            return organizationInfo;
        }

        public async Task GetCollectionsAsync(string organizationId, IAssetProject project, CancellationToken token, Action<IEnumerable<CollectionInfo>> onCompleted)
        {
            var assetCollections = project.ListCollectionsAsync(Range.All, token);
            var collections = new List<CollectionInfo>();
            await foreach (var assetCollection in assetCollections)
            {
                collections.Add(new CollectionInfo
                {
                    organizationId = organizationId,
                    projectId = project.Descriptor.ProjectId.ToString(),
                    name = assetCollection.Name,
                    parentPath = assetCollection.ParentPath
                });
            }

            onCompleted?.Invoke(collections);
        }

        public async IAsyncEnumerable<IAsset> SearchAsync(string organizationId, IEnumerable<string> projectIds,
            IAssetSearchFilter assetSearchFilter, int startIndex, int pageSize, [EnumeratorCancellation] CancellationToken token)
        {
            var range = new Range(startIndex, startIndex + pageSize);

            var devMode = Utilities.IsDevMode;
            if (devMode)
            {
                Debug.Log($"Fetching {range} Assets ...");
            }

            var t = new Stopwatch();
            t.Start();

            var count = 0;
            await foreach (var asset in m_CloudAssetsProxy.SearchAsync(organizationId, projectIds, assetSearchFilter, range, token))
            {
                yield return asset;
                ++count;
            }

            t.Stop();
            if (devMode)
            {
                Debug.Log($"Fetched {count} Assets from {range} in {t.ElapsedMilliseconds}ms");
            }
        }

        public async Task<Dictionary<string, string>> GetProjectIconUrlsAsync(string organizationId, CancellationToken token)
        {
            var organizations = await m_CloudAssetsProxy.GetOrganizationsAsync();
            var selectedOrg = organizations?.FirstOrDefault(o => o.Id.ToString() == organizationId);
            if (selectedOrg == null)
                return null;

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
            return await m_CloudAssetsProxy.GetFilterSelectionsAsync(organizationId, projectIds, searchFilter, groupBy, token);
        }

        public async Task<AssetComparisonResult> CompareAssetWithCloudAsync(IAssetData assetData, CancellationToken token)
        {
            if (assetData?.updated == null)
            {
                return AssetComparisonResult.Unknown;
            }

            try
            {
                var cloudAsset = await m_CloudAssetsProxy.GetAssetAsync(assetData.identifier.ToAssetDescriptor(), token);

                return assetData.updated == cloudAsset.AuthoringInfo.Updated
                    ? AssetComparisonResult.UpToDate
                    : AssetComparisonResult.OutDated;
            }
            catch (ForbiddenException)
            {
                return AssetComparisonResult.NotFoundOrInaccessible;
            }
            catch (Exception e)
            {
                Debug.LogException(e);

                return AssetComparisonResult.Unknown;
            }
        }

        public async Task EnableProjectAsync(CancellationToken token = default)
        {
            await m_CloudAssetsProxy.EnableProjectAsync(token);
        }

        private class CloudAssetsProxy
        {
            private readonly IUnityConnectProxy m_UnityConnect;

            public CloudAssetsProxy(IUnityConnectProxy unityConnect)
            {
                m_UnityConnect = unityConnect;
            }

            public async IAsyncEnumerable<IAssetProject> GetCurrentUserProjectList(string organizationId, Range range, [EnumeratorCancellation] CancellationToken token)
            {
                await foreach (var project in Services.AssetRepository.ListAssetProjectsAsync(new OrganizationId(organizationId), range, token))
                {
                    yield return project;
                }
            }

            public async Task<IAsset> GetAssetAsync(AssetDescriptor descriptor, CancellationToken token)
            {
                return await Services.AssetRepository.GetAssetAsync(descriptor, token);
            }

            public async Task<IEnumerable<IOrganization>> GetOrganizationsAsync()
            {
                var organizationsAsync = Services.OrganizationRepository.ListOrganizationsAsync(Range.All);
                var organizations = new List<IOrganization>();
                await foreach (var organization in organizationsAsync)
                {
                    organizations.Add(organization);
                }

                return organizations;
            }

            public async IAsyncEnumerable<IAsset> SearchAsync(string organizationId, IEnumerable<string> projectIds,
                IAssetSearchFilter assetSearchFilter, Range range, [EnumeratorCancellation] CancellationToken token)
            {
                var strongTypedOrgId = new OrganizationId(organizationId);
                var projectDescriptors = new List<ProjectDescriptor>();
                foreach (var projectId in projectIds)
                {
                    projectDescriptors.Add(new ProjectDescriptor(strongTypedOrgId, new ProjectId(projectId)));
                }

                var assetQueryBuilder = Services.AssetRepository.QueryAssets(projectDescriptors)
                    .LimitTo(range)
                    .SelectWhereMatchesFilter(assetSearchFilter);

                await foreach (var asset in assetQueryBuilder.ExecuteAsync(token))
                {
                    yield return asset;
                }
            }

            public async Task<List<string>> GetFilterSelectionsAsync(string organizationId, IEnumerable<string> projectIds,
                IAssetSearchFilter searchFilter, GroupableField groupBy, CancellationToken token)
            {
                var strongTypedOrgId = new OrganizationId(organizationId);
                var strongTypedProjectIds = projectIds.Select(p => new ProjectId(p)).ToList();

                var projectDescriptors = new List<ProjectDescriptor>();
                foreach (var projectId in strongTypedProjectIds)
                {
                    projectDescriptors.Add(new ProjectDescriptor(strongTypedOrgId, projectId));
                }

                var groupAndCountAssetsQueryBuilder = Services.AssetRepository.GroupAndCountAssets(projectDescriptors)
                    .SelectWhereMatchesFilter(searchFilter);
                var aggregation = await groupAndCountAssetsQueryBuilder.ExecuteAsync(groupBy, token);

                var result = aggregation.Keys.Distinct().ToList();
                result.Sort();

                return result;
            }

            public async Task EnableProjectAsync(CancellationToken token)
            {
                await Services.ProjectEnabler.EnableProjectAsync(m_UnityConnect.projectId, token);
            }
        }
    }

    public static class Services
    {
        static IAuthenticator s_Authenticator;
        static IAssetRepository s_AssetRepository;
        static ProjectEnabler s_ProjectEnabler;

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

        public static IOrganizationRepository OrganizationRepository
        {
            get
            {
                if (s_Authenticator == null)
                {
                    CreateServices();
                }

                return s_Authenticator;
            }
        }

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

        public static IAuthenticator Authenticator
        {
            get
            {
                if (s_Authenticator == null)
                {
                    CreateServices();
                }

                return s_Authenticator;
            }
        }

        public static AuthenticationState AuthenticationState { get; private set; }

        static void CreateServices()
        {
            if (s_AssetRepository != null && s_Authenticator != null)
                return;

            var pkgInfo = PackageInfo.FindForAssembly(Assembly.GetAssembly(typeof(Services)));
            var httpClient = new UnityHttpClient();
            var serviceHostResolver = UnityRuntimeServiceHostResolverFactory.Create();

            s_Authenticator = new UnityEditorAuthenticator(new TargetClientIdTokenToUnityServicesTokenExchanger(httpClient, serviceHostResolver));
            s_Authenticator.AuthenticationStateChanged += OnAuthenticationStateChanged;
            var serviceHttpClient = new ServiceHttpClient(httpClient, s_Authenticator, new AppIdProvider())
                .WithApiSourceHeaders(pkgInfo.name, pkgInfo.version);

            s_AssetRepository = AssetRepositoryFactory.Create(serviceHttpClient, serviceHostResolver);

            s_ProjectEnabler = new ProjectEnabler(serviceHttpClient, serviceHostResolver);
        }

        static void OnAuthenticationStateChanged(AuthenticationState state)
        {
            AuthenticationState = state;
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
