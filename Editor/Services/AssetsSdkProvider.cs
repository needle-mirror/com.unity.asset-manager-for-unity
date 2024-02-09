using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Assets;
using Unity.Cloud.Common;
using Unity.Cloud.Common.Runtime;
using Unity.Cloud.Identity;
using Unity.Cloud.Identity.Editor;
using System.Reflection;
using Unity.Cloud.Identity.Runtime;
using UnityEditor;
using Debug = UnityEngine.Debug;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Unity.AssetManager.Editor
{
    internal interface IAssetsProvider : IService
    {
        Task<OrganizationInfo> GetOrganizationInfoAsync(string organizationId, CancellationToken token);
        IAsyncEnumerable<IAsset> SearchAsync(CollectionInfo collectionInfo, IEnumerable<string> searchFilters, int startIndex, int pageSize, CancellationToken token);
        IAsyncEnumerable<IAsset> SearchAsync(IEnumerable<AssetIdentifier> assetIds, CancellationToken token);
        IAsyncEnumerable<IAsset> SearchAsync(OrganizationInfo organizationInfo, IEnumerable<string> searchFilters, int startIndex, int pageSize, CancellationToken token);
        Task<Dictionary<string, string>> GetProjectIconUrlsAsync(string organizationId, CancellationToken token);
    }

    [Serializable]
    class AssetsSdkProvider : BaseService<IAssetsProvider>, IAssetsProvider
    {
        private static readonly Pagination k_DefaultPagination = new(nameof(IAsset.Name), Range.All);

        private readonly CloudAssetsProxy m_CloudAssetsProxy;
        private readonly IAssetDataManager m_AssetDataManager;
        private readonly IUnityConnectProxy m_UnityConnectProxy;

        public AssetsSdkProvider(IAssetDataManager assetDataManager, IUnityConnectProxy unityConnectProxy)
        {
            m_AssetDataManager = RegisterDependency(assetDataManager);
            m_UnityConnectProxy = RegisterDependency(unityConnectProxy);
            m_CloudAssetsProxy = new CloudAssetsProxy(m_UnityConnectProxy);
        }

        public async Task<OrganizationInfo> GetOrganizationInfoAsync(string organizationId, CancellationToken token)
        {
            var t = new Stopwatch();
            t.Start();
            
            var tasks = new List<Task>();
            var organizationInfo = new OrganizationInfo { id = organizationId };

            var count = 0;
            await foreach (var project in m_CloudAssetsProxy.GetCurrentUserProjectList(organizationId, k_DefaultPagination, token))
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
            if (EditorPrefs.GetBool("DeveloperMode", false))
            {
                Debug.Log($"Fetching {count} Projects took {t.ElapsedMilliseconds}ms");
            }

            return organizationInfo;
        }

        public async Task GetCollectionsAsync(string organizationId, IAssetProject project, CancellationToken token, Action<IEnumerable<CollectionInfo>> onCompleted)
        {
            var assetCollections = await project.ListCollectionsAsync(token);
            var collections = assetCollections.Select(assetCollection =>
                new CollectionInfo
                {
                    organizationId = organizationId,
                    projectId = project.Descriptor.ProjectId.ToString(),
                    name = assetCollection.Name,
                    parentPath = assetCollection.ParentPath
                });
            onCompleted?.Invoke(collections);
        }

        public async IAsyncEnumerable<IAsset> SearchAsync(CollectionInfo collectionInfo, IEnumerable<string> searchFilters, int startIndex, int pageSize, [EnumeratorCancellation] CancellationToken token)
        {
            var assetSearchFilter = DefaultAssetSearchFilter(searchFilters);
            
            var collectionPath = collectionInfo.GetFullPath();
            if (!string.IsNullOrEmpty(collectionPath))
            {
                assetSearchFilter.Collections.Add(new CollectionPath(collectionPath));
            }
            
            await foreach (var asset in SearchAsync(collectionInfo.organizationId, new List<string> { collectionInfo.projectId }, assetSearchFilter, startIndex, pageSize, token))
            {
                yield return asset;
            }
        }

        public async IAsyncEnumerable<IAsset> SearchAsync(OrganizationInfo organizationInfo, IEnumerable<string> searchFilters, int startIndex, int pageSize, [EnumeratorCancellation] CancellationToken token)
        {
            var projectIds = organizationInfo.projectInfos.Select(p => p.id).ToList();

            await foreach (var asset in SearchAsync(organizationInfo.id, projectIds, DefaultAssetSearchFilter(searchFilters), startIndex, pageSize, token))
            {
                yield return asset;
            }
        }
        
        async IAsyncEnumerable<IAsset> SearchAsync(string organizationId, IEnumerable<string> projectIds, IAssetSearchFilter assetSearchFilter, int startIndex, int pageSize, [EnumeratorCancellation] CancellationToken token)
        {
            var pagination = new Pagination(nameof(IAsset.Name), new Range(startIndex, startIndex + pageSize));

            var devMode = EditorPrefs.GetBool("DeveloperMode", false);
            if (devMode)
            {
                Debug.Log($"Fetching {pagination.Range} Assets ...");
            }
            
            var t = new Stopwatch();
            t.Start();
            
            var count = 0;
            await foreach (var asset in m_CloudAssetsProxy.SearchAsync(organizationId, projectIds, assetSearchFilter, pagination, token))
            {
                yield return asset;
                ++count;
            }
            
            t.Stop();
            if (devMode)
            {
                Debug.Log($"Fetching {count} Assets from {pagination.Range} took {t.ElapsedMilliseconds}ms");
            }
        }

        AssetSearchFilter DefaultAssetSearchFilter(IEnumerable<string> searchFilters)
        {
            var searchFilterString = string.Join(" ", searchFilters);
            
            var assetSearchFilter = new AssetSearchFilter
            {
                IncludedFields = new FieldsFilter { AssetFields = AssetFields.previewFileUrl }
            };

            assetSearchFilter.Name.ForAny(searchFilterString);
            assetSearchFilter.Description.ForAny(searchFilterString);
            assetSearchFilter.Tags.ForAny(searchFilterString);

            return assetSearchFilter;
        }

        public async IAsyncEnumerable<IAsset> SearchAsync(IEnumerable<AssetIdentifier> assetIdentifiers, [EnumeratorCancellation] CancellationToken token)
        {
            foreach (var assetIdentifier in assetIdentifiers)
            {
                var assetData = m_AssetDataManager.GetAssetData(assetIdentifier);
                if (assetData == null)
                {
                    var assetVersionDescriptor = new AssetDescriptor(
                        new ProjectDescriptor(
                            new OrganizationId(assetIdentifier.organizationId),
                            new ProjectId(assetIdentifier.projectId)),
                        new AssetId(assetIdentifier.sourceId),
                        new AssetVersion(assetIdentifier.version));

                    var fieldsFilter = new FieldsFilter { AssetFields = AssetFields.previewFileUrl };
                    var asset = await m_CloudAssetsProxy.CreateCloudAssetDataWithFilesAsync(assetVersionDescriptor, fieldsFilter, token);
                    yield return asset;
                }
            }
        }

        public async Task<Dictionary<string, string>> GetProjectIconUrlsAsync(string organizationId, CancellationToken token)
        {
            var organizations = await m_CloudAssetsProxy.GetOrganizationsAsync(token);
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

        private class CloudAssetsProxy
        {
            private Task m_InitializeSDKTask;

            private readonly IUnityConnectProxy m_UnityConnect;
            
            public CloudAssetsProxy(IUnityConnectProxy unityConnect)
            {
                m_UnityConnect = unityConnect;
            }
            
            private async Task InitializeAndCheckAuthenticationState()
            {
                m_InitializeSDKTask ??= Services.InitializeAuthenticatorAsync();
                await m_InitializeSDKTask;

                // Most of the UI and other services are relying on UnityConnect events to know user login status change, but the SDK uses UnityEditorAuthenticator
                // So we need to do a special handling here for when UnityConnect tells us that the user is logged in but the UnityEditorAuthenticator is saying otherwise
                while (m_UnityConnect.isUserLoggedIn && Services.AuthenticationState != AuthenticationState.LoggedIn)
                    await Task.Delay(50);
            }

            public async IAsyncEnumerable<IAssetProject> GetCurrentUserProjectList(string organizationId, Pagination pagination, [EnumeratorCancellation] CancellationToken token)
            {
                await InitializeAndCheckAuthenticationState();

                await foreach (var project in Services.AssetRepository.ListAssetProjectsAsync(new OrganizationId(organizationId), pagination, token))
                {
                    yield return project;
                }
            }

            public async Task<IAsset> CreateCloudAssetDataWithFilesAsync(AssetDescriptor assetVersionDescriptor, FieldsFilter fieldsFilter, CancellationToken token)
            {
                await InitializeAndCheckAuthenticationState();
                return await Services.AssetRepository.GetAssetAsync(assetVersionDescriptor, fieldsFilter, token);
            }
            
            public async Task<IEnumerable<IOrganization>> GetOrganizationsAsync(CancellationToken token)
            {
                await InitializeAndCheckAuthenticationState();
                return await Services.OrganizationRepository.ListOrganizationsAsync();
            }

            public async IAsyncEnumerable<IAsset> SearchAsync(string organizationId, IEnumerable<string> projectIds, IAssetSearchFilter assetSearchFilter, Pagination pagination, [EnumeratorCancellation] CancellationToken token)
            {
                await InitializeAndCheckAuthenticationState();

                var orgId = new OrganizationId(organizationId);
                var prjIds = projectIds.Select(p => new ProjectId(p)).ToList();

                await foreach (var asset in Services.AssetRepository.SearchAssetsAsync(orgId, prjIds, assetSearchFilter, pagination, token))
                {
                    yield return asset;
                }
                
            }
        }
    }

    public static class Services
    {
        static IOrganizationRepository s_OrganizationRepository;
        static ICompositeAuthenticator s_CompositeAuthenticator;
        static IAuthenticator s_Authenticator;
        static IAssetRepository s_AssetRepository;

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
                if (s_OrganizationRepository == null)
                {
                    CreateServices();    
                }
                return s_OrganizationRepository;
            }
        }
        
        public static AuthenticationState AuthenticationState { get; private set; }

        static void CreateServices()
        {
            if (s_AssetRepository != null && s_OrganizationRepository != null && s_Authenticator != null)
                return;
                
            var pkgInfo = PackageInfo.FindForAssembly(Assembly.GetAssembly(typeof(Services)));
            var httpClient = new UnityHttpClient();
            var serviceHostResolver = UnityRuntimeServiceHostResolverFactory.Create();

            s_Authenticator = new UnityEditorAuthenticator(new TargetClientIdTokenToUnityServicesTokenExchanger(httpClient, serviceHostResolver));
            s_Authenticator.AuthenticationStateChanged += OnAuthenticationStateChanged;
            var serviceHttpClient = new ServiceHttpClient(httpClient, s_Authenticator, new AppIdProvider())
                .WithApiSourceHeaders(pkgInfo.name, pkgInfo.version);

            s_OrganizationRepository = new AuthenticatorOrganizationRepository(serviceHttpClient, serviceHostResolver);

            s_AssetRepository = AssetRepositoryFactory.Create(serviceHttpClient, serviceHostResolver);
        }

        public static async Task InitializeAuthenticatorAsync()
        {
            CreateServices();

            await s_Authenticator.InitializeAsync();
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
