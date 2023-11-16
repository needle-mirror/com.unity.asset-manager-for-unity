using System;
using System.Collections.Generic;
using System.IO;
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
using UnityEditor.PackageManager;
using UnityEngine;


namespace Unity.AssetManager.Editor
{
    internal interface IAssetsProvider : IService
    {
        Task<OrganizationInfo> GetOrganizationInfoAsync(string organizationId, CancellationToken token);
        Task<IReadOnlyCollection<AssetIdentifier>> SearchAsync(CollectionInfo collectionInfo, IEnumerable<string> searchFilters, int startIndex, int pageSize, CancellationToken token);
        Task<IReadOnlyCollection<AssetIdentifier>> SearchAsync(IEnumerable<AssetIdentifier> assetIds, CancellationToken token);
        Task UpdateAssetDownloadUrlsAsync(IAssetData assetData, CancellationToken token);
    }

    [Serializable]
    class AssetsSdkProvider : BaseService<IAssetsProvider>, IAssetsProvider
    {
        private const string k_PublishedKeyword = "Published";
        private static readonly Pagination k_DefaultPagination = new(nameof(IAsset.Name), new Range(0, Constants.DefaultPageSize));
        
        private AssetData.AssetDataFactory m_AssetDataFactory;
        
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
            var organizations = await m_CloudAssetsProxy.GetOrganizationsAsync(token);
            var selectedOrg = organizations?.FirstOrDefault(o => o.Id.ToString() == organizationId);
            if (selectedOrg == null)
                return null;

            var organizationInfo = new OrganizationInfo{ id = organizationId};
            await foreach (var project in m_CloudAssetsProxy.GetCurrentUserProjectList(selectedOrg.Id.ToString(), k_DefaultPagination, token))
            {
                if (project == null)
                    continue;

                var collections = await GetCollectionsAsync(organizationId, project, token);
                var projectInfo = new ProjectInfo
                {
                    id = project.Descriptor.ProjectId.ToString(),
                    name = project.Name,
                    collectionInfos = collections
                };
                organizationInfo.projectInfos.Add(projectInfo);
            }
            return organizationInfo;
        }

        public async Task<List<CollectionInfo>> GetCollectionsAsync(string organizationId, IAssetProject project, CancellationToken token)
        {
            var assetCollections = await project.ListCollectionsAsync(token);
            var collections = assetCollections.Select(assetCollection =>
                new CollectionInfo
                {
                    organizationId = organizationId,
                    projectId = project.Descriptor.ProjectId.ToString(),
                    name = assetCollection.Name,
                    parentPath = assetCollection.ParentPath
                }).ToList();
            return collections;
        }

        public async Task<IReadOnlyCollection<AssetIdentifier>> SearchAsync(CollectionInfo collectionInfo, IEnumerable<string> searchFilters, int startIndex, int pageSize, CancellationToken token)
        {
            var result = new List<AssetIdentifier>();
            var searchFilterString = string.Join(" ", searchFilters);
            var pagination = new Pagination(nameof(IAsset.Name), new Range(startIndex, startIndex + pageSize));

            var assetSearchFilter = new AssetSearchFilter();
            var collectionPath = collectionInfo.GetFullPath();
            if (!string.IsNullOrEmpty(collectionPath))
                assetSearchFilter.Collections.Add(new CollectionPath(collectionPath));

            assetSearchFilter.IncludedFields = new FieldsFilter { AssetFields = AssetFields.all };
            assetSearchFilter.Status.Include(k_PublishedKeyword);
            assetSearchFilter.Name.ForAny(searchFilterString);
            assetSearchFilter.Description.ForAny(searchFilterString);
            assetSearchFilter.Tags.ForAny(searchFilterString);

            m_AssetDataFactory ??= new AssetData.AssetDataFactory();
            var assetDatas = new List<IAssetData>();
            await foreach (var cloudAssetData in m_CloudAssetsProxy.SearchAsync(
                               collectionInfo.organizationId,
                               new List<string> { collectionInfo.projectId },
                               assetSearchFilter, pagination, token))
            {
                var assetData = m_AssetDataFactory.CreateAssetData(cloudAssetData);
                assetDatas.Add(assetData);
                result.Add(assetData.id);
            }
            m_AssetDataManager.AddOrUpdateAssetDataFromCloudAsset(assetDatas);
            return result;
        }

        public async Task<IReadOnlyCollection<AssetIdentifier>> SearchAsync(IEnumerable<AssetIdentifier> assetIdentifiers, CancellationToken token)
        {
            m_AssetDataFactory ??= new AssetData.AssetDataFactory();
            var result = new List<AssetIdentifier>();
            var newAssetDatas = new List<IAssetData>();
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

                    var fieldsFilter = new FieldsFilter { AssetFields = AssetFields.all };
                    var cloudAssetData = await m_CloudAssetsProxy.CreateCloudAssetDataWithFilesAsync(assetVersionDescriptor, fieldsFilter, token);
                    assetData = m_AssetDataFactory.CreateAssetData(cloudAssetData);
                    m_AssetDataManager.UpdateFilesStatus(assetData, AssetDataFilesStatus.Fetched, false);
                    newAssetDatas.Add(assetData);
                }
                result.Add(assetData.id);
            }
            m_AssetDataManager.AddOrUpdateAssetDataFromCloudAsset(newAssetDatas);
            return result;
        }

        public async Task UpdateAssetDownloadUrlsAsync(IAssetData assetData, CancellationToken token)
        {
            try
            {
                if (assetData.filesInfosStatus == AssetDataFilesStatus.NotFetched)
                {
                    m_AssetDataFactory ??= new AssetData.AssetDataFactory();
                    m_AssetDataManager.UpdateFilesStatus(assetData, AssetDataFilesStatus.BeingFetched);
                    var cloudAssetData = await m_CloudAssetsProxy.UpdateAssetDownloadUrlsAsync(assetData, token);
                    var updatedAssetData = m_AssetDataFactory.CreateAssetData(cloudAssetData);
                    m_AssetDataManager.UpdateAssetDataFileInfos(updatedAssetData);
                }
            }
            catch (ForbiddenException e)
            {
                m_AssetDataManager.UpdateFilesStatus(assetData, AssetDataFilesStatus.NotFetched);
                Debug.LogError(e);
            }
            catch (TimeoutException e)
            {
                m_AssetDataManager.UpdateFilesStatus(assetData, AssetDataFilesStatus.NotFetched);
                Debug.LogError(e);
            }
        }

        private class CloudAssetsProxy
        {
            private IOrganizationRepository m_OrganizationRepository;
            private ICompositeAuthenticator m_CompositeAuthenticator;
            private IAuthenticator m_Authenticator;
            private IAssetRepository m_AssetRepository;
            private AuthenticationState m_AuthenticationState;

            private Task m_InitializeSDKTask;

            private readonly IUnityConnectProxy m_UnityConnect;
            public CloudAssetsProxy(IUnityConnectProxy unityConnect)
            {
                m_UnityConnect = unityConnect;
            }

            private async Task InitializeSDK()
            {
                var pkgInfo = PackageInfo.FindForAssembly(Assembly.GetAssembly(typeof(CloudAssetsProxy)));
                var httpClient = new UnityHttpClient();
                var serviceHostResolver = UnityRuntimeServiceHostResolverFactory.Create();
                var playerSettings = UnityCloudPlayerSettings.Instance;

                m_Authenticator = new UnityEditorAuthenticator(new TargetClientIdTokenToUnityServicesTokenExchanger(httpClient, serviceHostResolver));
                m_Authenticator.AuthenticationStateChanged += OnAuthenticationStateChanged;
                var serviceHttpClient = new ServiceHttpClient(httpClient, m_Authenticator, playerSettings)
                    .WithApiSourceHeaders(pkgInfo.name, pkgInfo.version);

                m_OrganizationRepository = new AuthenticatorOrganizationRepository(serviceHttpClient, serviceHostResolver);

                m_AssetRepository = AssetRepositoryFactory.Create(serviceHttpClient, serviceHostResolver);

                await m_Authenticator.InitializeAsync();
            }

            private async Task InitializeAndCheckAuthenticationState()
            {
                m_InitializeSDKTask ??= InitializeSDK();
                await m_InitializeSDKTask;

                // Most of the UI and other services are relying on UnityConnect events to know user login status change, but the SDK uses UnityEditorAuthenticator
                // So we need to do a special handling here for when UnityConnect tells us that the user is logged in but the UnityEditorAuthenticator is saying otherwise
                while (m_UnityConnect.isUserLoggedIn && m_AuthenticationState != AuthenticationState.LoggedIn)
                    await Task.Delay(50);
            }

            private void OnAuthenticationStateChanged(AuthenticationState state)
            {
                m_AuthenticationState = state;
            }

            public async IAsyncEnumerable<IAssetProject> GetCurrentUserProjectList(string organizationId, Pagination pagination, [EnumeratorCancellation] CancellationToken token)
            {
                await InitializeAndCheckAuthenticationState();

                await foreach (var project in m_AssetRepository.ListAssetProjectsAsync(new OrganizationId(organizationId), pagination, token))
                {
                    yield return project;
                }
            }

            public async Task<IEnumerable<IOrganization>> GetOrganizationsAsync(CancellationToken token)
            {
                await InitializeAndCheckAuthenticationState();
                return await m_OrganizationRepository.ListOrganizationsAsync();
            }

            public async Task<CloudAssetData> CreateCloudAssetDataWithFilesAsync(AssetDescriptor assetVersionDescriptor, FieldsFilter fieldsFilter, CancellationToken token)
            {
                await InitializeAndCheckAuthenticationState();
                var asset = await m_AssetRepository.GetAssetAsync(assetVersionDescriptor, fieldsFilter, token);

                var cloudAssetData = await CreateCloudAssetDataAsync(asset,
                    assetVersionDescriptor.ProjectDescriptor.OrganizationGenesisId.ToString(),
                    assetVersionDescriptor.ProjectDescriptor.ProjectId,
                    token);
                cloudAssetData.filesArg = await GetCloudAssetDataFiles(asset, token);

                return cloudAssetData;
            }

            public async IAsyncEnumerable<CloudAssetData> SearchAsync(string organizationId, IEnumerable<string> projectIds, IAssetSearchFilter assetSearchFilter, Pagination pagination, [EnumeratorCancellation] CancellationToken token)
            {
                await InitializeAndCheckAuthenticationState();

                var orgId = new OrganizationId(organizationId);
                var prjIds = projectIds.Select(p => new ProjectId(p)).ToList();
                var cloudAssetDatas = new List<CloudAssetData>();

                await foreach (var asset in m_AssetRepository.SearchAssetsAsync(orgId, prjIds, assetSearchFilter,  pagination, token))
                {
                    var projectId = prjIds.FirstOrDefault(p => p == asset.SourceProject.ProjectId);
                    var cloudAssetData = await CreateCloudAssetDataAsync(asset, organizationId, projectId, token);
                    cloudAssetDatas.Add(cloudAssetData);
                    yield return cloudAssetData;
                }
            }

            public async Task<CloudAssetData> UpdateAssetDownloadUrlsAsync(IAssetData assetData, CancellationToken token)
            {
                await InitializeAndCheckAuthenticationState();

                var assetDescriptor =  new AssetDescriptor(
                    new ProjectDescriptor(
                        new OrganizationId(assetData.id.organizationId),
                        new ProjectId(assetData.id.projectId)),
                    new AssetId(assetData.id.sourceId),
                    new AssetVersion(assetData.id.version));

                var fieldsFilter = new FieldsFilter();
                fieldsFilter.AssetFields = AssetFields.all;
                fieldsFilter.FileFields = FileFields.all;
                
                return await CreateCloudAssetDataWithFilesAsync(assetDescriptor, fieldsFilter, token);
            }

            private async Task<CloudAssetData> CreateCloudAssetDataAsync(IAsset asset, string organizationId, ProjectId projectId, CancellationToken token)
            {
                Uri fileUri = null;
                if (!string.IsNullOrEmpty(asset.PreviewFile))
                    fileUri = await asset.GetPreviewFileDownloadUrlAsync(token); // in the normal case, we will not hit the backend here. even if it's async.

                return new CloudAssetData(asset, organizationId, projectId.ToString(), fileUri);
            }

            private async Task<IEnumerable<CloudAssetDataFile>> GetCloudAssetDataFiles(IAsset asset, CancellationToken token)
            {
                var cloudFiles = new List<IFile>();
                var cloudAssetDataFiles = new List<CloudAssetDataFile>();
                await foreach (var file in asset.ListFilesAsync(Range.All, token)) // TODO: what if we have more than some large number of files...
                    cloudFiles.Add(file);

                foreach (var cloudFile in cloudFiles)
                {
                    var uri = await cloudFile.GetDownloadUrlAsync(token);
                    var cloudAssetDataFile = new CloudAssetDataFile(cloudFile, uri.ToString());
                    cloudAssetDataFiles.Add(cloudAssetDataFile);
                }

                return cloudAssetDataFiles;
            }
        }
    }
}

