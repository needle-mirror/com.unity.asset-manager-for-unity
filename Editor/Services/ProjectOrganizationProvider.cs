using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class OrganizationInfo
    {
        public string id;
        public List<ProjectInfo> projectInfos = new();
    }

    [Serializable]
    internal class ProjectInfo
    {
        public string id;
        public string name;
        public List<CollectionInfo> collectionInfos = new();
    }

    interface IProjectOrganizationProvider : IService
    {
        event Action<OrganizationInfo> OrganizationChanged;
        event Action<ProjectInfo, CollectionInfo> ProjectSelectionChanged;
        OrganizationInfo SelectedOrganization { get; }
        ProjectInfo SelectedProject { get; }
        CollectionInfo SelectedCollection { get; }
        void SelectProject(ProjectInfo projectInfo, string collectionPath = null);
        void SelectProject(string projectId, string collectionPath = null);
        void EnableProjectForAssetManager();
        bool isLoading { get; }
        ErrorOrMessageHandlingData errorOrMessageHandlingData { get; } // TODO Error reporting should be an event
    }

    [Serializable]
    internal class ProjectOrganizationProvider : BaseService<IProjectOrganizationProvider>, IProjectOrganizationProvider
    {
        private static readonly string k_NoOrganizationMessage = L10n.Tr("It seems your project is not linked to an organization. Please link your project to a Unity project ID to start using the Asset Manager service.");
        private static readonly string k_CurrentProjectNotEnabledMessage = L10n.Tr("It seems your current project is not enabled for use in the Asset Manager.");

        public event Action<OrganizationInfo> OrganizationChanged;
        public event Action<ProjectInfo, CollectionInfo> ProjectSelectionChanged;

        [SerializeField]
        private AsyncLoadOperation m_LoadOrganizationOperation = new();

        public bool isLoading => m_LoadOrganizationOperation.isLoading;

        [SerializeField]
        private OrganizationInfo m_OrganizationInfo;

        public OrganizationInfo SelectedOrganization => isLoading || string.IsNullOrEmpty(m_OrganizationInfo?.id) ? null : m_OrganizationInfo;

        [SerializeField]
        private string m_SelectedProjectId;

        [SerializeField]
        string m_CollectionPath;

        static readonly string k_ProjectPrefKey = "com.unity.asset-manager-for-unity.selectedProjectId";
        static readonly string k_CollectionPathPrefKey = "com.unity.asset-manager-for-unity.selectedCollectionPath";

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

        public ProjectInfo SelectedProject
        {
            get
            {
                return m_OrganizationInfo?.projectInfos?.Find(p => p.id == m_SelectedProjectId);
            }
        }

        public CollectionInfo SelectedCollection
        {
            get
            {
                var collection = SelectedProject?.collectionInfos.Find(c => c.GetFullPath() == m_CollectionPath);

                if (collection != null)
                    return collection;

                return new CollectionInfo
                {
                    organizationId = SelectedOrganization?.id,
                    projectId = SelectedProject?.id
                };
            }
        }

        public void SelectProject(ProjectInfo projectInfo, string collectionPath = null)
        {
            SelectProject(projectInfo?.id, collectionPath);
        }

        public void SelectProject(string projectId, string collectionPath = null)
        {
            var currentProjectId = SelectedProject?.id;

            if (string.IsNullOrEmpty(projectId) && string.IsNullOrEmpty(currentProjectId))
                return;

            if (currentProjectId == projectId && (m_CollectionPath ?? string.Empty) == (collectionPath ?? string.Empty))
                return;

            if (!string.IsNullOrEmpty(currentProjectId) && !m_OrganizationInfo.projectInfos.Exists(p => p.id == currentProjectId))
            {
                Debug.LogError($"Project with id '{currentProjectId}' is not part of the organization '{m_OrganizationInfo.id}'");
                return;
            }

            m_SelectedProjectId = projectId;
            m_CollectionPath = collectionPath;

            SavedProjectId = m_SelectedProjectId;
            SavedCollectionPath = m_CollectionPath;

            ProjectSelectionChanged?.Invoke(SelectedProject, SelectedCollection);
        }

        [SerializeField]
        private ErrorOrMessageHandlingData m_ErrorOrMessageHandling = new();

        public ErrorOrMessageHandlingData errorOrMessageHandlingData => m_ErrorOrMessageHandling;

        [SerializeReference]
        IUnityConnectProxy m_UnityConnectProxy;

        [SerializeReference]
        IAssetsProvider m_AssetsProvider;

        [ServiceInjection]
        public void Inject(IUnityConnectProxy unityConnectProxy, IAssetsProvider assetsProvider)
        {
            m_UnityConnectProxy = unityConnectProxy;
            m_AssetsProvider = assetsProvider;
        }

        public override void OnEnable()
        {
            m_UnityConnectProxy.onOrganizationIdChange += OnProjectStateChanged;
            FetchProjectOrganization(m_UnityConnectProxy.organizationId);
        }

        public override void OnDisable()
        {
            m_UnityConnectProxy.onOrganizationIdChange -= OnProjectStateChanged;
        }

        void OnProjectStateChanged(string newOrgId)
        {
            FetchProjectOrganization(newOrgId);
        }

        public async void EnableProjectForAssetManager()
        {
            await m_AssetsProvider.EnableProjectAsync();

            FetchProjectOrganization(m_UnityConnectProxy.organizationId, true);
        }

        void FetchProjectOrganization(string newOrgId, bool forceRefresh = false)
        {
            if (!forceRefresh && !string.IsNullOrEmpty(m_OrganizationInfo?.id) && m_OrganizationInfo.id == newOrgId)
                return;

            if (m_OrganizationInfo?.id != newOrgId)
            {
                m_OrganizationInfo = new OrganizationInfo { id = newOrgId };
            }

            if (isLoading)
            {
                m_LoadOrganizationOperation.Cancel();
            }

            Utilities.DevLog($"Fetching organization info for '{newOrgId}'...");

            if (string.IsNullOrEmpty(newOrgId))
            {
                m_ErrorOrMessageHandling.message = k_NoOrganizationMessage;
                m_ErrorOrMessageHandling.errorOrMessageRecommendedAction = ErrorOrMessageRecommendedAction.OpenServicesSettingButton;
                InvokeOrganizationChanged();
                return;
            }

            _ = m_LoadOrganizationOperation.Start(token => m_AssetsProvider.GetOrganizationInfoAsync(newOrgId, token),
                onLoadingStartCallback: () =>
                {
                    errorOrMessageHandlingData.message = string.Empty;
                    errorOrMessageHandlingData.errorOrMessageRecommendedAction = ErrorOrMessageRecommendedAction.Retry;
                    InvokeOrganizationChanged(); // TODO Should use a different event
                },
                onCancelledCallback: InvokeOrganizationChanged,
                onExceptionCallback: e =>
                {
                    Debug.LogException(e);
                    errorOrMessageHandlingData.message = L10n.Tr("It seems there was an error while trying to retrieve assets.");
                    errorOrMessageHandlingData.errorOrMessageRecommendedAction = ErrorOrMessageRecommendedAction.Retry;
                    InvokeOrganizationChanged(); // TODO Send exception event
                },
                onSuccessCallback: result =>
                {
                    m_OrganizationInfo = result;
                    if (m_OrganizationInfo?.projectInfos.Any() == false)
                    {
                        SelectProject(string.Empty);
                        errorOrMessageHandlingData.message = k_CurrentProjectNotEnabledMessage;
                        errorOrMessageHandlingData.errorOrMessageRecommendedAction = ErrorOrMessageRecommendedAction.EnableProject;
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
                return SelectedProject ?? m_OrganizationInfo.projectInfos.FirstOrDefault();
            }

            var saveProjectInfo = m_OrganizationInfo.projectInfos.Find(p => p.id == savedProjectId);
            return saveProjectInfo ?? m_OrganizationInfo.projectInfos.FirstOrDefault();
        }

        string RestoreSelectedCollection()
        {
            return SavedCollectionPath;
        }

        void InvokeOrganizationChanged()
        {
            var selected = SelectedOrganization;

            if (Utilities.IsDevMode)
            {
                Debug.Log($"OrganizationChanged '{selected?.id}'");
            }

            OrganizationChanged?.Invoke(selected);
        }
    }
}
