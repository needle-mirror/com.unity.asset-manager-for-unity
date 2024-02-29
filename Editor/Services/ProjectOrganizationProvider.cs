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

        static readonly ProjectInfo s_ProjectInfoAllAssets = new()
        {
            id = "AllAssets",
            name = Constants.AllAssetsFolderName
        };

        internal static ProjectInfo AllAssetsProjectInfo => s_ProjectInfoAllAssets;
    }

    interface IProjectOrganizationProvider : IService
    {
        event Action<OrganizationInfo> OrganizationChanged;
        event Action<ProjectInfo, CollectionInfo> ProjectSelectionChanged;
        OrganizationInfo SelectedOrganization { get; }
        ProjectInfo SelectedProject { get; }
        CollectionInfo SelectedCollection { get; }
        void SelectProject(ProjectInfo projectInfo, string collectionPath = null);
        bool isLoading { get; }
        ErrorOrMessageHandlingData errorOrMessageHandlingData { get; } // TODO Error reporting should be an event
    }

    [Serializable]
    internal class ProjectOrganizationProvider : BaseService<IProjectOrganizationProvider>, IProjectOrganizationProvider
    {
        private static readonly string k_NoOrganizationMessage = L10n.Tr("It seems your project is not linked to an organization. Please link your project to a Unity project ID to start using the Asset Manager service.");
        private static readonly string k_NoProjectsMessage = L10n.Tr("It seems you don't have any projects created in your Asset Manager Dashboard.");

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
                return m_SelectedProjectId == ProjectInfo.AllAssetsProjectInfo.id ?
                    ProjectInfo.AllAssetsProjectInfo : m_OrganizationInfo?.projectInfos?.Find(p => p.id == m_SelectedProjectId);
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
            var oldSelectedProjectId = SelectedProject?.id ?? string.Empty;
            var newSelectedProjectId = projectInfo?.id ?? string.Empty;

            if (oldSelectedProjectId == newSelectedProjectId && m_CollectionPath == collectionPath)
                return;

            m_SelectedProjectId = newSelectedProjectId;
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
            m_UnityConnectProxy.onOrganizationIdChange += OnOrganizationIdChange;
            FetchProjectOrganization(m_UnityConnectProxy.organizationId);
        }

        public override void OnDisable()
        {
            m_UnityConnectProxy.onOrganizationIdChange -= OnOrganizationIdChange;
        }

        private void OnOrganizationIdChange(string newOrgId)
        {
            FetchProjectOrganization(newOrgId);
        }

        private void FetchProjectOrganization(string newOrgId)
        {
            if (!string.IsNullOrEmpty(m_OrganizationInfo?.id) && m_OrganizationInfo.id == newOrgId)
                return;

            if (isLoading)
            {
                m_LoadOrganizationOperation.Cancel();
            }

            if (Utilities.IsDevMode)
            {
                Debug.Log($"Fetching organization info for '{newOrgId}'...");
            }

            m_OrganizationInfo = new OrganizationInfo { id = newOrgId };
            if (string.IsNullOrEmpty(newOrgId))
            {
                m_ErrorOrMessageHandling.message = k_NoOrganizationMessage;
                m_ErrorOrMessageHandling.errorOrMessageRecommendedAction = ErrorOrMessageRecommendedAction.OpenServicesSettingButton;
                InvokeOrganizationChanged();
                return;
            }

            m_LoadOrganizationOperation.Start(token => m_AssetsProvider.GetOrganizationInfoAsync(newOrgId, token),
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
                    if (m_OrganizationInfo?.projectInfos.Any() == true)
                    {
                        SelectProject(RestoreSelectedProject(), RestoreSelectedCollection());
                    }
                    else
                    {
                        SelectProject(null, null);
                        errorOrMessageHandlingData.message = k_NoProjectsMessage;
                        errorOrMessageHandlingData.errorOrMessageRecommendedAction = ErrorOrMessageRecommendedAction.OpenAssetManagerDashboardLink;
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

            if (ProjectInfo.AllAssetsProjectInfo.id == savedProjectId)
            {
                return ProjectInfo.AllAssetsProjectInfo;
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
