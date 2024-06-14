using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.Identity;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class OrganizationInfo
    {
        public string Id;
        public List<ProjectInfo> ProjectInfos = new();

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
        public string Id;
        public string Name;
        public List<CollectionInfo> CollectionInfos = new();
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
        ErrorOrMessageHandlingData ErrorOrMessageHandlingData { get; } // TODO Error reporting should be an event

        event Action<OrganizationInfo> OrganizationChanged;
        event Action<ProjectInfo, CollectionInfo> ProjectSelectionChanged;

        void SelectProject(ProjectInfo projectInfo, string collectionPath = null);
        void SelectProject(string projectId, string collectionPath = null);
        void EnableProjectForAssetManager();
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

        [SerializeField]
        ErrorOrMessageHandlingData m_ErrorOrMessageHandling = new();

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

        public bool IsLoading => m_LoadOrganizationOperation.IsLoading;

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
                var collection = SelectedProject?.CollectionInfos.Find(c => c.GetFullPath() == m_CollectionPath);

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
            Services.AuthenticationStateChanged += OnAuthenticationStateChanged;
            m_UnityConnectProxy.OrganizationIdChanged += OnProjectStateChanged;
            m_UnityConnectProxy.OnCloudServicesReachabilityChanged += OnCloudServicesReachabilityChanged;
        }

        public override void OnDisable()
        {
            Services.AuthenticationStateChanged -= OnAuthenticationStateChanged;
            m_UnityConnectProxy.OrganizationIdChanged -= OnProjectStateChanged;
            m_UnityConnectProxy.OnCloudServicesReachabilityChanged -= OnCloudServicesReachabilityChanged;
        }

        void OnAuthenticationStateChanged()
        {
            if (Services.AuthenticationState.Equals(AuthenticationState.LoggedIn))
            {
                FetchProjectOrganization(m_UnityConnectProxy.OrganizationId);
            }
        }

        void OnCloudServicesReachabilityChanged(bool cloudServicesReachable)
        {
            if (cloudServicesReachable)
            {
                FetchProjectOrganization(m_UnityConnectProxy.OrganizationId);
            }
        }

        public void SelectProject(ProjectInfo projectInfo, string collectionPath = null)
        {
            SelectProject(projectInfo?.Id, collectionPath);
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

        public ErrorOrMessageHandlingData ErrorOrMessageHandlingData => m_ErrorOrMessageHandling;

        public async void EnableProjectForAssetManager()
        {
            await m_AssetsProvider.EnableProjectAsync();

            FetchProjectOrganization(m_UnityConnectProxy.OrganizationId, true);
        }

        void OnProjectStateChanged(string newOrgId)
        {
            FetchProjectOrganization(newOrgId);
        }

        void FetchProjectOrganization(string newOrgId, bool forceRefresh = false)
        {
            if (!m_UnityConnectProxy.AreCloudServicesReachable || !Services.AuthenticationState.Equals(AuthenticationState.LoggedIn))
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
                    m_ErrorOrMessageHandling.Message = k_NoConnectionMessage;
                    m_ErrorOrMessageHandling.ErrorOrMessageRecommendedAction = ErrorOrMessageRecommendedAction.None;
                }
                else
                {
                    m_ErrorOrMessageHandling.Message = k_NoOrganizationMessage;
                    m_ErrorOrMessageHandling.ErrorOrMessageRecommendedAction =
                        ErrorOrMessageRecommendedAction.OpenServicesSettingButton;
                }

                InvokeOrganizationChanged();
                return;
            }

            _ = m_LoadOrganizationOperation.Start(token => m_AssetsProvider.GetOrganizationInfoAsync(newOrgId, token),
                () =>
                {
                    ErrorOrMessageHandlingData.Message = string.Empty;
                    ErrorOrMessageHandlingData.ErrorOrMessageRecommendedAction = ErrorOrMessageRecommendedAction.Retry;
                    InvokeOrganizationChanged(); // TODO Should use a different event
                },
                cancelledCallback: InvokeOrganizationChanged,
                exceptionCallback: e =>
                {
                    Debug.LogException(e);

                    if (!m_UnityConnectProxy.AreCloudServicesReachable)
                    {
                        m_ErrorOrMessageHandling.Message = k_NoConnectionMessage;
                        m_ErrorOrMessageHandling.ErrorOrMessageRecommendedAction = ErrorOrMessageRecommendedAction.None;
                    }
                    else
                    {
                        m_ErrorOrMessageHandling.Message = k_ErrorRetrievingOrganization;
                        m_ErrorOrMessageHandling.ErrorOrMessageRecommendedAction =
                            ErrorOrMessageRecommendedAction.Retry;
                    }

                    InvokeOrganizationChanged(); // TODO Send exception event
                },
                successCallback: result =>
                {
                    m_OrganizationInfo = result;
                    if (m_OrganizationInfo?.ProjectInfos.Any() == false)
                    {
                        SelectProject(string.Empty);
                        ErrorOrMessageHandlingData.Message = k_CurrentProjectNotEnabledMessage;
                        ErrorOrMessageHandlingData.ErrorOrMessageRecommendedAction =
                            ErrorOrMessageRecommendedAction.EnableProject;
                    }
                    else
                    {
                        SelectProject(RestoreSelectedProject(), RestoreSelectedCollection());
                    }

                    InvokeOrganizationChanged();
                });
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
    }
}
