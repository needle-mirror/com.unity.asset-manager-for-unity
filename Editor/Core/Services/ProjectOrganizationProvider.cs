using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.AssetManager.Core.Editor
{
    [Serializable]
    class OrganizationInfo
    {
        public string Id;
        public string Name;
        public List<ProjectInfo> ProjectInfos = new();
        public List<MetadataFieldDefinition> MetadataFieldDefinitions = new();

        bool m_IsUserInfosLoading;
        List<UserInfo> m_UserInfos;
        List<Action<List<UserInfo>>> m_UserInfosWaitingCallbacks = new();

        public async Task GetUserInfosAsync(Action<List<UserInfo>> callback)
        {
            if (m_UserInfos != null)
            {
                callback?.Invoke(m_UserInfos);
            }

            m_UserInfosWaitingCallbacks.Add(callback);

            if (m_IsUserInfosLoading)
            {
                return;
            }

            m_IsUserInfosLoading = true;

            var userInfos = new List<UserInfo>();
            await foreach (var member in ServicesContainer.instance.Resolve<IAssetsProvider>()
                               .GetOrganizationMembersAsync(Id, Range.All, CancellationToken.None))
            {
                userInfos.Add(new UserInfo { UserId = member.UserId.ToString(), Name = member.Name });
            }

            foreach (var waitingCallback in m_UserInfosWaitingCallbacks)
            {
                waitingCallback?.Invoke(userInfos);
            }

            m_UserInfosWaitingCallbacks.Clear();
            m_UserInfos = userInfos;
        }
    }

    [Serializable]
    class ProjectInfo
    {
        [SerializeField]
        List<CollectionInfo> m_CollectionInfos;

        public string Id;
        public string Name;
        public IEnumerable<CollectionInfo> CollectionInfos => m_CollectionInfos;
        public event Action<ProjectInfo> OnCollectionsUpdated;

        public void SetCollections(IEnumerable<CollectionInfo> collections)
        {
            m_CollectionInfos = collections?.ToList();
            OnCollectionsUpdated?.Invoke(this);
        }

        public CollectionInfo GetCollection(string collectionPath)
        {
            return m_CollectionInfos?.Find(c => c.GetFullPath() == collectionPath);
        }
    }

    [Serializable]
    class UserInfo
    {
        public string UserId;
        public string Name;
    }

    interface IProjectOrganizationProvider : IService
    {
        OrganizationInfo SelectedOrganization { get; }
        ProjectInfo SelectedProject { get; }
        CollectionInfo SelectedCollection { get; }
        bool IsLoading { get; }
        MessageData MessageData { get; }
        event Action<MessageData> MessageThrown;
        event Action<OrganizationInfo> OrganizationChanged;
        event Action<bool> LoadingStateChanged;
        event Action<ProjectInfo, CollectionInfo> ProjectSelectionChanged;
        event Action<ProjectInfo> ProjectInfoChanged;

        void SelectProject(ProjectInfo projectInfo, string collectionPath = null, bool updateProject = false);
        void SelectProject(string projectId, string collectionPath = null);
        void EnableProjectForAssetManager();
        ProjectInfo GetProject(string projectId);
        Task CreateCollection(CollectionInfo collectionInfo);
        Task DeleteCollection(CollectionInfo collectionInfo);
        Task RenameCollection(CollectionInfo collectionInfo, string newName);
    }

    [Serializable]
    class ProjectOrganizationProvider : BaseService<IProjectOrganizationProvider>, IProjectOrganizationProvider
    {
        [SerializeField]
        AsyncLoadOperation m_LoadOrganizationOperation = new();

        [SerializeField]
        OrganizationInfo m_OrganizationInfo;

        [SerializeField]
        string m_SelectedProjectId;

        [SerializeField]
        string m_CollectionPath;

        [FormerlySerializedAs("m_ErrorOrMessageHandling")]
        [SerializeField]
        MessageData m_MessageData = new();

        [SerializeReference]
        IAssetsProvider m_AssetsProvider;

        [SerializeReference]
        IUnityConnectProxy m_UnityConnectProxy;

        static readonly string k_NoOrganizationMessage =
            L10n.Tr(
                "It seems your project is not linked to an organization. Please link your project to a Unity project ID to start using the Asset Manager service.");
        static readonly string k_CurrentProjectNotEnabledMessage =
            L10n.Tr("It seems your current project is not enabled for use in the Asset Manager.");
        static readonly string k_ProjectPrefKey = "com.unity.asset-manager-for-unity.selectedProjectId";
        static readonly string k_CollectionPathPrefKey = "com.unity.asset-manager-for-unity.selectedCollectionPath";
        static readonly string k_NoConnectionMessage =
            L10n.Tr("No network connection. Please check your internet connection.");
        static readonly string k_ErrorRetrievingOrganization =
            L10n.Tr("It seems there was an error while trying to retrieve organization info.");

        string SavedProjectId
        {
            set => EditorPrefs.SetString(k_ProjectPrefKey, value);
            get => EditorPrefs.GetString(k_ProjectPrefKey, null);
        }

        string SavedCollectionPath
        {
            set => EditorPrefs.SetString(k_CollectionPathPrefKey, value);
            get => EditorPrefs.GetString(k_CollectionPathPrefKey, null);
        }

        public event Action<OrganizationInfo> OrganizationChanged;
        public event Action<ProjectInfo, CollectionInfo> ProjectSelectionChanged;
        public event Action<ProjectInfo> ProjectInfoChanged;

        public bool IsLoading => m_LoadOrganizationOperation.IsLoading;
        public MessageData MessageData => m_MessageData;
        public event Action<MessageData> MessageThrown;

        public event Action<bool> LoadingStateChanged;

        public OrganizationInfo SelectedOrganization =>
            IsLoading || string.IsNullOrEmpty(m_OrganizationInfo?.Id) ? null : m_OrganizationInfo;

        public ProjectInfo SelectedProject
        {
            get { return m_OrganizationInfo?.ProjectInfos?.Find(p => p.Id == m_SelectedProjectId); }
        }

        public CollectionInfo SelectedCollection
        {
            get
            {
                var collection = SelectedProject?.GetCollection(m_CollectionPath);

                if (collection != null)
                {
                    return collection;
                }

                return new CollectionInfo
                {
                    OrganizationId = SelectedOrganization?.Id,
                    ProjectId = SelectedProject?.Id
                };
            }
        }

        [ServiceInjection]
        public void Inject(IUnityConnectProxy unityConnectProxy, IAssetsProvider assetsProvider)
        {
            m_UnityConnectProxy = unityConnectProxy;
            m_AssetsProvider = assetsProvider;
        }

        public override void OnEnable()
        {
            m_AssetsProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
            m_UnityConnectProxy.OrganizationIdChanged += OnProjectStateChanged;
            m_UnityConnectProxy.CloudServicesReachabilityChanged += OnCloudServicesReachabilityChanged;
        }

        public override void OnDisable()
        {
            m_AssetsProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
            m_UnityConnectProxy.OrganizationIdChanged -= OnProjectStateChanged;
            m_UnityConnectProxy.CloudServicesReachabilityChanged -= OnCloudServicesReachabilityChanged;
        }

        void OnAuthenticationStateChanged(AuthenticationState newState)
        {
            if (newState == AuthenticationState.LoggedIn)
            {
                FetchProjectOrganization(m_UnityConnectProxy.OrganizationId);
            }
        }

        void OnCloudServicesReachabilityChanged(bool cloudServicesReachable)
        {
            if (cloudServicesReachable && m_UnityConnectProxy.HasValidOrganizationId)
            {
                FetchProjectOrganization(m_UnityConnectProxy.OrganizationId);
            }
        }

        public void SelectProject(ProjectInfo projectInfo, string collectionPath = null, bool updateProject = false)
        {
            SelectProject(projectInfo?.Id, collectionPath);

            if (updateProject)
            {
                TaskUtils.TrackException(GetProjectAsync(projectInfo?.Id));
            }
        }

        public void SelectProject(string projectId, string collectionPath = null)
        {
            var currentProjectId = SelectedProject?.Id;

            if (string.IsNullOrEmpty(projectId) && string.IsNullOrEmpty(currentProjectId))
                return;

            if (!string.IsNullOrEmpty(currentProjectId) &&
                !m_OrganizationInfo.ProjectInfos.Exists(p => p.Id == currentProjectId))
            {
                Debug.LogError(
                    $"Project with id '{currentProjectId}' is not part of the organization '{m_OrganizationInfo.Id}'");
                return;
            }

            m_SelectedProjectId = projectId;
            m_CollectionPath = collectionPath;

            SavedProjectId = m_SelectedProjectId;
            SavedCollectionPath = m_CollectionPath;

            ProjectSelectionChanged?.Invoke(SelectedProject, SelectedCollection);
        }

        public async void EnableProjectForAssetManager()
        {
            await m_AssetsProvider.EnableProjectAsync();

            FetchProjectOrganization(m_UnityConnectProxy.OrganizationId, true);
        }

        public ProjectInfo GetProject(string projectId)
        {
            return m_OrganizationInfo?.ProjectInfos.Find(p => p.Id == projectId);
        }

        public async Task CreateCollection(CollectionInfo collectionInfo)
        {
            if (collectionInfo == null)
                return;

            await m_AssetsProvider.CreateCollectionAsync(collectionInfo, CancellationToken.None);

            var projectInfo = await GetProjectAsync(collectionInfo.ProjectId);
            if (projectInfo == null)
                return;

            var collections = projectInfo.CollectionInfos.ToList();
            collections.Add(collectionInfo);
            projectInfo.SetCollections(collections);
            ProjectInfoChanged?.Invoke(projectInfo);

            SelectProject(projectInfo, collectionInfo.GetFullPath());
        }

        public async Task DeleteCollection(CollectionInfo collectionInfo)
        {
            if(collectionInfo == null)
                return;

            await m_AssetsProvider.DeleteCollectionAsync(collectionInfo, CancellationToken.None);

            var projectInfo = await GetProjectAsync(collectionInfo.ProjectId);
            if (projectInfo == null)
                return;

            var collections = projectInfo.CollectionInfos.ToList();
            collections.RemoveAll(c => c.GetFullPath().Contains(collectionInfo.GetFullPath()));
            projectInfo.SetCollections(collections);
            ProjectInfoChanged?.Invoke(projectInfo);
        }

        public async Task RenameCollection(CollectionInfo collectionInfo, string newName)
        {
            if(collectionInfo == null || string.IsNullOrEmpty(newName))
                return;

            var localProjectInfo = GetProject(collectionInfo.ProjectId);
            if(localProjectInfo.CollectionInfos.Any(c => c.GetFullPath() == $"{collectionInfo.ParentPath}/{newName}"))
            {
                ProjectInfoChanged?.Invoke(localProjectInfo);
                throw new ServiceException(L10n.Tr("A collection with the same name already exists."));
            }

            bool isCollectionSelected = m_CollectionPath == collectionInfo.GetFullPath();
            bool isChildCollectionSelected = m_CollectionPath.StartsWith(collectionInfo.GetFullPath());

            await m_AssetsProvider.RenameCollectionAsync(collectionInfo, newName, CancellationToken.None);

            var projectInfo = await GetProjectAsync(collectionInfo.ProjectId);
            if (projectInfo == null)
                return;

            var childCollectionSelectedSubPath = m_CollectionPath.Remove(0,collectionInfo.GetFullPath().Length);
            collectionInfo.Name = newName;

            var collections = projectInfo.CollectionInfos.ToList();
            var renamedCollection = collections.Find(c => c.GetFullPath() == collectionInfo.GetFullPath());

            ProjectInfoChanged?.Invoke(projectInfo);

            if (renamedCollection != null)
            {
                if (isCollectionSelected)
                {
                    SelectProject(projectInfo.Id, renamedCollection.GetFullPath());
                }
                else if (isChildCollectionSelected)
                {
                    SelectProject(projectInfo.Id, renamedCollection.GetFullPath() + childCollectionSelectedSubPath);
                }
            }
        }

        void OnProjectStateChanged(string newOrgId)
        {
            FetchProjectOrganization(newOrgId);
        }

        void FetchProjectOrganization(string newOrgId, bool forceRefresh = false)
        {
            if (!m_UnityConnectProxy.AreCloudServicesReachable ||
                m_AssetsProvider.AuthenticationState != AuthenticationState.LoggedIn)
                return;

            if (!forceRefresh && !string.IsNullOrEmpty(m_OrganizationInfo?.Id) && m_OrganizationInfo.Id == newOrgId)
                return;

            if (m_OrganizationInfo?.Id != newOrgId)
            {
                m_OrganizationInfo = new OrganizationInfo { Id = newOrgId };
            }

            if (IsLoading)
            {
                m_LoadOrganizationOperation.Cancel();
            }

            Utilities.DevLog($"Fetching organization info for '{newOrgId}'...");

            if (string.IsNullOrEmpty(newOrgId))
            {
                if (!m_UnityConnectProxy.AreCloudServicesReachable)
                {
                    RaiseNoConnectionErrorMessage();
                }
                else
                {
                    m_MessageData.Message = k_NoOrganizationMessage;
                    m_MessageData.RecommendedAction = RecommendedAction.OpenServicesSettingButton;
                    m_MessageData.IsPageScope = true;
                }

                InvokeOrganizationChanged();
                return;
            }

            _ = m_LoadOrganizationOperation.Start(token => m_AssetsProvider.GetOrganizationInfoAsync(newOrgId, token),
                () =>
                {
                    LoadingStateChanged?.Invoke(true);
                    m_MessageData.Message = string.Empty;
                    m_MessageData.RecommendedAction = RecommendedAction.Retry;
                    m_MessageData.IsPageScope = true;
                    InvokeOrganizationChanged(); // TODO Should use a different event
                },
                cancelledCallback: InvokeOrganizationChanged,
                exceptionCallback: e =>
                {
                    Debug.LogException(e);

                    if (!m_UnityConnectProxy.AreCloudServicesReachable)
                    {
                        RaiseNoConnectionErrorMessage();
                    }
                    else
                    {
                        m_MessageData.Message = k_ErrorRetrievingOrganization;
                        m_MessageData.RecommendedAction = RecommendedAction.Retry;
                        m_MessageData.IsPageScope = true;
                    }

                    InvokeOrganizationChanged(); // TODO Send exception event
                },
                successCallback: result =>
                {
                    m_OrganizationInfo = result;
                    if (m_OrganizationInfo?.ProjectInfos.Any() == false)
                    {
                        m_MessageData.Message = k_CurrentProjectNotEnabledMessage;
                        m_MessageData.RecommendedAction = RecommendedAction.EnableProject;
                        m_MessageData.IsPageScope = true;
                    }
                    else
                    {
                        SelectProject(RestoreSelectedProject(), RestoreSelectedCollection());
                    }

                    InvokeOrganizationChanged();
                },
                finallyCallback: () => { LoadingStateChanged?.Invoke(false); }
            );
        }

        void RaiseNoConnectionErrorMessage()
        {
            MessageData messageData = new()
            {
                Message = k_NoConnectionMessage,
                RecommendedAction = RecommendedAction.None
            };
            MessageThrown?.Invoke(messageData);
        }

        ProjectInfo RestoreSelectedProject()
        {
            var savedProjectId = SavedProjectId;

            if (string.IsNullOrEmpty(savedProjectId))
            {
                return SelectedProject ?? m_OrganizationInfo.ProjectInfos.FirstOrDefault();
            }

            var saveProjectInfo = m_OrganizationInfo.ProjectInfos.Find(p => p.Id == savedProjectId);
            return saveProjectInfo ?? m_OrganizationInfo.ProjectInfos.FirstOrDefault();
        }

        string RestoreSelectedCollection()
        {
            return SavedCollectionPath;
        }

        void InvokeOrganizationChanged()
        {
            var selected = SelectedOrganization;

            Utilities.DevLog($"OrganizationChanged '{selected?.Id}'");

            OrganizationChanged?.Invoke(selected);
        }

        async Task<ProjectInfo> GetProjectAsync(string projectId)
        {
            if (string.IsNullOrEmpty(projectId))
                return null;

            var projectInfo = await m_AssetsProvider.GetProjectInfoAsync(SelectedOrganization.Id, projectId, CancellationToken.None);
            if (projectInfo == null)
                return null;

            var index = m_OrganizationInfo.ProjectInfos.FindIndex(pi => pi.Id == projectId);
            if (index == -1)
            {
                m_OrganizationInfo.ProjectInfos.Add(projectInfo);
            }
            else
            {
                m_OrganizationInfo.ProjectInfos[index] = projectInfo;
            }
            ProjectInfoChanged?.Invoke(projectInfo);

            return projectInfo;
        }
    }
}
