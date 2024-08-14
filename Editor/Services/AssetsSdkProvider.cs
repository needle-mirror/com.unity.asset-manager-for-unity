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
    
    interface IAssetsProvider : IService
    {
        // Authentication
        
        event Action<AuthenticationState> AuthenticationStateChanged;
        AuthenticationState AuthenticationState { get; }

        // Organizations
        
        Task<OrganizationInfo> GetOrganizationInfoAsync(string organizationId, CancellationToken token);
        
        IAsyncEnumerable<IOrganization> ListOrganizationsAsync(Range range, CancellationToken token);
        
        Task<IOrganization> GetOrganizationAsync(string organizationId);
       
        Task<StorageUsage> GetOrganizationCloudStorageUsageAsync(IOrganization organization, CancellationToken token = default);
       
        IAsyncEnumerable<IMemberInfo> GetOrganizationMembersAsync(string organizationId, Range range,
            CancellationToken token);
        
        // Projects
        
        Task<Dictionary<string, string>> GetProjectIconUrlsAsync(string organizationId, CancellationToken token);
       
        Task EnableProjectAsync(CancellationToken token = default);
        
        // Assets 
        
        Task<AssetData> GetAssetAsync(AssetIdentifier assetIdentifier, CancellationToken token);
        Task<AssetData> GetLatestAssetVersionAsync(AssetIdentifier assetIdentifier, CancellationToken token);
        
        IAsyncEnumerable<AssetData> SearchAsync(string organizationId, IEnumerable<string> projectIds,
             AssetSearchFilter assetSearchFilter, int startIndex, int pageSize, CancellationToken token);
        Task<List<string>> GetFilterSelectionsAsync(string organizationId, IEnumerable<string> projectIds,
            AssetSearchFilter assetSearchFilter,
            AssetSearchGroupBy groupBy, CancellationToken token);

        Task<AssetData> CreateAssetAsync(ProjectIdentifier projectIdentifier, AssetCreation assetCreation, CancellationToken token);
        Task<AssetData> CreateUnfrozenVersionAsync(AssetData assetData, CancellationToken token);

        Task RemoveAsset(AssetIdentifier assetIdentifier, CancellationToken token);
        
        Task UpdateAsync(AssetData assetData, AssetUpdate assetUpdate, CancellationToken token);
        Task UpdateStatusAsync(AssetData assetData, AssetStatusAction statusAction, CancellationToken token);
        Task FreezeAsync(AssetData assetData, string changeLog, CancellationToken token);
        Task RefreshAsync(AssetData assetData, CancellationToken token);
        
        Task<AssetComparisonResult> CompareAssetWithCloudAsync(IAssetData assetData, CancellationToken token);
        
        // Files

        Task<IReadOnlyDictionary<string, Uri>> GetAssetDownloadUrlsAsync(AssetData assetData, IProgress<FetchDownloadUrlsProgress> progress, CancellationToken token);

        Task<AssetDataFile> UploadThumbnail(AssetData assetData, Texture2D thumbnail, IProgress<HttpProgress> progress, CancellationToken token);
        Task RemoveThumbnail(AssetData assetData, CancellationToken token);

        Task<AssetDataFile> UploadFile(AssetData assetData, string destinationPath, Stream stream, IProgress<HttpProgress> progress, CancellationToken token);
        Task RemoveAllFiles(AssetData assetData, CancellationToken token);
        
        // Miscs
        
        void OnAfterDeserializeAssetData(AssetData assetData);
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
        
        [SerializeReference]
        IUnityConnectProxy m_UnityConnectProxy;

        [SerializeReference]
        ISettingsManager m_SettingsManager;
        
        IAssetRepository m_AssetRepositoryOverride;
        IAssetRepository AssetRepository
        {
            get
            {
                if (m_AssetRepositoryOverride != null)
                {
                    return m_AssetRepositoryOverride;
                }

                return Services.AssetRepository;
            }
        }

        public AssetsSdkProvider()
        { }

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
            AssetSearchFilter assetSearchFilter, int startIndex, int pageSize,
            [EnumeratorCancellation] CancellationToken token)
        {
            var cloudAssetSearchFilter = Map(assetSearchFilter);
            
            var range = new Range(startIndex, startIndex + pageSize);

            Utilities.DevLog($"Fetching {range} Assets ...");

            var t = new Stopwatch();
            t.Start();

            var count = 0;
            await foreach (var asset in SearchAsync(organizationId, projectIds, cloudAssetSearchFilter, range, token))
            {
                yield return asset;
                ++count;
            }

            t.Stop();

            Utilities.DevLog($"Fetched {count} Assets from {range} in {t.ElapsedMilliseconds}ms");
        }

        public async Task<StorageUsage> GetOrganizationCloudStorageUsageAsync(IOrganization organization, CancellationToken token = default)
        {
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
            
            // Temporary fix to ensure all data is up to date; fix coming in later SDK release
            if (asset != null)
            {
                await asset.RefreshAsync(token);
            }

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
            var asset = await AssetRepository.GetAssetAsync(Map(assetIdentifier), token);
            return asset == null ? null : new AssetData(asset);
        }

        public async Task<AssetData> GetLatestAssetVersionAsync(AssetIdentifier assetIdentifier, CancellationToken token)
        {
            if (assetIdentifier == null)
            {
                return null;
            }
            
            var projectDescriptor = new ProjectDescriptor(new OrganizationId(assetIdentifier.OrganizationId),
                new ProjectId(assetIdentifier.ProjectId));
            var assetId = new AssetId(assetIdentifier.AssetId);
            
            var project = await AssetRepository.GetAssetProjectAsync(projectDescriptor, token);
            var asset = await project.GetAssetWithLatestVersionAsync(assetId, token);
            return new AssetData(asset);
        }

        public async Task<AssetComparisonResult> CompareAssetWithCloudAsync(IAssetData assetData, CancellationToken token)
        {
            if (assetData == null)
            {
                return AssetComparisonResult.Unknown;
            }

            try
            {
                var assetIdentifier = assetData.Identifier;

                AssetData cloudAsset = null;
                try
                {
                    cloudAsset = await GetLatestAssetVersionAsync(assetIdentifier, token);
                }
                catch (NotFoundException)
                {
                    try
                    {
                        if (assetData is AssetData typedAssetData)
                        {
                            await using var enumerator = typedAssetData.GetAssetDataInDescendingVersionNumberOrder(token).GetAsyncEnumerator(token);
                            var latestVersion = await enumerator.MoveNextAsync() ? enumerator.Current : default;
                            cloudAsset = latestVersion;
                        }
                    }
                    catch (NotFoundException)
                    {
                        cloudAsset = null;
                    }
                }

                if (cloudAsset == null)
                {
                    return AssetComparisonResult.NotFoundOrInaccessible;
                }

                return assetIdentifier == cloudAsset.Identifier && assetData.Updated != null &&
                       assetData.Updated == cloudAsset.Updated
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

        Task<IDataset> GetPreviewDatasetAsync(AssetData assetData, CancellationToken token)
        {
            const string previewTag = "Preview";
            return GetDatasetAsync(assetData, previewTag, token);
        }

        Task<IDataset> GetSourceDatasetAsync(AssetData assetData, CancellationToken token)
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

        public Task UpdateAsync(AssetData assetData, AssetUpdate assetUpdate, CancellationToken token)
        {
            var cloudAssetUpdate = Map(assetUpdate);
            return assetData?.Asset == null ? Task.CompletedTask : assetData.Asset.UpdateAsync(cloudAssetUpdate, token);
        }

        public Task UpdateStatusAsync(AssetData assetData, AssetStatusAction statusAction, CancellationToken token)
        {
            var cloudStatusAction = Map(statusAction);
            return assetData?.Asset == null ? Task.CompletedTask : assetData.Asset.UpdateStatusAsync(cloudStatusAction, token);
        }

        public Task FreezeAsync(AssetData assetData, string changeLog, CancellationToken token)
        {
            return assetData?.Asset == null ? Task.CompletedTask : assetData.Asset.FreezeAsync(changeLog, token);
        }

        public Task RefreshAsync(AssetData assetData, CancellationToken token)
        {
            return assetData?.Asset == null ? Task.CompletedTask : assetData.Asset.RefreshAsync(token);
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
                catch (ServiceException e)
                {
                    if (e.StatusCode != HttpStatusCode.NotFound)
                        throw;
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

                var deleteTasks = new List<Task>();

                foreach (var file in filesToWipe)
                {
                    deleteTasks.Add(dataset.RemoveFileAsync(file.Descriptor.Path, token));
                }

                await Task.WhenAll(deleteTasks);
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
            if (assetData == null || assetData.Asset == null)
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
            // Although the method is obsolete, we still need to use it to count the number of files in the asset;
            // this can be removed once V2 end points become available as we will be able to simplify to use only GetAssetDownloadUrlsSingleRequestAsync with proper pagination
            
            // Count the asset because if the asset has < threshold files, we can retrieve all urls in a single calls
            int fileCount = 0; 
            await foreach (var _ in assetData.Asset.ListFilesAsync(..(k_MaxNumberOfFilesForAssetDownloadUrlFetch+1), token))
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
                result = await GetAssetDownloadUrlsSingleRequestAsync(assetData, sourceDataset, progress, token);
            }

            return result;
        }

        async Task<Dictionary<string, Uri>> GetAssetDownloadUrlsSingleRequestAsync(AssetData assetData, IDataset sourceDataset, IProgress<FetchDownloadUrlsProgress> progress, CancellationToken token)
        {
            if (assetData?.Asset == null || sourceDataset == null)
            {
                return null;
            }
            
            var result = new Dictionary<string, Uri>();

            // Requesting all files url for the whole asset.
            progress?.Report(new FetchDownloadUrlsProgress("Downloading all urls at once", 0.2f));
            var downloadUrls = await assetData.Asset.GetAssetDownloadUrlsAsync(token);
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
        
        public void OnAfterDeserializeAssetData(AssetData assetData)
        {
            if (!string.IsNullOrEmpty(assetData.AssetSerialized))
            {
                assetData.Asset = AssetRepository.DeserializeAsset(assetData.AssetSerialized);
            }
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
        
        static AuthenticationState Map(Unity.Cloud.Identity.AuthenticationState authenticationState) =>
            authenticationState switch
            {
                Unity.Cloud.Identity.AuthenticationState.AwaitingInitialization => AuthenticationState.AwaitingInitialization,
                Unity.Cloud.Identity.AuthenticationState.AwaitingLogin => AuthenticationState.AwaitingLogin,
                Unity.Cloud.Identity.AuthenticationState.LoggedIn => AuthenticationState.LoggedIn,
                Unity.Cloud.Identity.AuthenticationState.AwaitingLogout => AuthenticationState.AwaitingLogout,
                Unity.Cloud.Identity.AuthenticationState.LoggedOut => AuthenticationState.LoggedOut,
                _ => throw new ArgumentOutOfRangeException(nameof(authenticationState), authenticationState, null)
            };
        
        static IAssetSearchFilter Map(AssetSearchFilter assetSearchFilter)
        {
            var cloudAssetSearchFilter = new Cloud.Assets.AssetSearchFilter();

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
                cloudAssetSearchFilter.Collections.WhereContains( new CollectionPath(assetSearchFilter.Collection));
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
        
        static Unity.Cloud.Assets.AssetCreation Map(AssetCreation assetCreation)
        {
            if (assetCreation == null)
            {
                return null;
            }
            
            return new Unity.Cloud.Assets.AssetCreation(assetCreation.Name)
            {
                Type = Map(assetCreation.Type),
                Collections = assetCreation.Collections?.Select(x => new CollectionPath(x)).ToList(),
                Tags = assetCreation.Tags
            };
        }
        
        static Unity.Cloud.Assets.AssetStatusAction Map(AssetStatusAction statusAction)
        {
            switch (statusAction)
            {
                case AssetStatusAction.Approve:
                    return Unity.Cloud.Assets.AssetStatusAction.Approve;
                case AssetStatusAction.SendForReview:
                    return Unity.Cloud.Assets.AssetStatusAction.SendForReview;
                case AssetStatusAction.Withdraw:
                    return Unity.Cloud.Assets.AssetStatusAction.Withdraw;
                case AssetStatusAction.Publish:
                    return Unity.Cloud.Assets.AssetStatusAction.Publish;
                case AssetStatusAction.Reject:
                    return Unity.Cloud.Assets.AssetStatusAction.Reject;
            }

            throw new ArgumentOutOfRangeException(nameof(statusAction), statusAction, null);
        }

        static IAssetUpdate Map(AssetUpdate assetUpdate)
        {
            if (assetUpdate == null)
            {
                return null;
            }

            return new Unity.Cloud.Assets.AssetUpdate()
            {
                Name = assetUpdate.Name,
                Type = Map(assetUpdate.Type),
                PreviewFile = assetUpdate.PreviewFile,
                Tags = assetUpdate.Tags
            };
        }

        static Cloud.Assets.AssetType Map(AssetType assetType)
        {
            return assetType switch
            {
                AssetType.Asset2D => Cloud.Assets.AssetType.Asset_2D,
                AssetType.Model3D => Cloud.Assets.AssetType.Model_3D,
                AssetType.Audio => Cloud.Assets.AssetType.Audio,
                AssetType.Material => Cloud.Assets.AssetType.Material,
                AssetType.Script => Cloud.Assets.AssetType.Script,
                AssetType.Video => Cloud.Assets.AssetType.Video,
                _ => Cloud.Assets.AssetType.Other
            };
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
