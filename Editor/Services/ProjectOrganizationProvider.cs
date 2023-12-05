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

    interface IProjectOrganizationProvider: IService
    {
        // This event gets triggered when organization info changed, or when the loading for organizationInfo starts or finished.
        // We combine these two events because loading start/finish usually happens together with an organization info change.
        // If we have separate events, some UIs will need to hook on to all those events and call refresh multiple times in a row.
        event Action<OrganizationInfo, bool> onOrganizationInfoOrLoadingChanged;
        event Action<ProjectInfo> onProjectSelectionChanged;
        bool isLoading { get; }
        OrganizationInfo organization { get; }
        ProjectInfo selectedProject { get; set; }
        ErrorOrMessageHandlingData errorOrMessageHandlingData { get; }
        void RefreshProjects();
    }

    [Serializable]
    internal class ProjectOrganizationProvider : BaseService<IProjectOrganizationProvider>, IProjectOrganizationProvider
    {
        private static readonly string k_NoOrganizationMessage =
            L10n.Tr("It seems your project is not linked to an organization. Please link your project to a Unity project ID to start using the Asset Manager service.");
        private static readonly string k_NoProjectsMessage = L10n.Tr("It seems you don't have any projects created in your Asset Manager Dashboard.");

        public event Action<OrganizationInfo, bool> onOrganizationInfoOrLoadingChanged;
        public event Action<ProjectInfo> onProjectSelectionChanged;

        [SerializeField]
        private AsyncLoadOperation m_LoadOrganizationOperation = new();
        public bool isLoading => m_LoadOrganizationOperation.isLoading;

        [SerializeField]
        private OrganizationInfo m_OrganizationInfo;
        public OrganizationInfo organization => isLoading || string.IsNullOrEmpty(m_OrganizationInfo?.id) ? null : m_OrganizationInfo;

        [SerializeField]
        private string m_SelectedProjectId;
        public ProjectInfo selectedProject
        {
            get => m_OrganizationInfo?.projectInfos?.FirstOrDefault( p => p.id == m_SelectedProjectId);
            set
            {
                var oldSelectedProjectId = selectedProject?.id ?? string.Empty;
                var newSelectedProjectId = value?.id ?? string.Empty;
                if (oldSelectedProjectId == newSelectedProjectId)
                    return;
                m_SelectedProjectId = newSelectedProjectId;
                onProjectSelectionChanged?.Invoke(selectedProject);
            }
        }

        [SerializeField]
        private ErrorOrMessageHandlingData m_ErrorOrMessageHandling = new();
        public ErrorOrMessageHandlingData errorOrMessageHandlingData => m_ErrorOrMessageHandling;

        private readonly IUnityConnectProxy m_UnityConnectProxy;
        private readonly IAssetsProvider m_AssetsProvider;
        public ProjectOrganizationProvider(IUnityConnectProxy unityConnectProxy, IAssetsProvider assetsProvider)
        {
            m_UnityConnectProxy = RegisterDependency(unityConnectProxy);
            m_AssetsProvider = RegisterDependency(assetsProvider);

            m_ErrorOrMessageHandling.message = k_NoOrganizationMessage;
            m_ErrorOrMessageHandling.errorOrMessageRecommendedAction = ErrorOrMessageRecommendedAction.OpenServicesSettingButton;
        }

        public override void OnEnable()
        {
            OnOrganizationIdChange(m_UnityConnectProxy.organizationId);
            m_UnityConnectProxy.onOrganizationIdChange += OnOrganizationIdChange;
        }

        public override void OnDisable()
        {
            m_UnityConnectProxy.onOrganizationIdChange -= OnOrganizationIdChange;
        }

        public void RefreshProjects()
        {
            FetchProjectOrganization(m_OrganizationInfo?.id, true);
        }

        private void OnOrganizationIdChange(string newOrgId)
        {
            FetchProjectOrganization(newOrgId);
        }

        private void FetchProjectOrganization(string newOrgId, bool refreshProjects = false)
        {
            var currentOrgId = m_OrganizationInfo?.id ?? string.Empty;
            newOrgId ??= string.Empty;
            if (currentOrgId == newOrgId && !refreshProjects)
                return;

            if (isLoading)
                m_LoadOrganizationOperation.Cancel();

            m_OrganizationInfo = new OrganizationInfo { id = newOrgId };
            if (string.IsNullOrEmpty(newOrgId))
            {
                m_ErrorOrMessageHandling.message = k_NoOrganizationMessage;
                m_ErrorOrMessageHandling.errorOrMessageRecommendedAction = ErrorOrMessageRecommendedAction.OpenServicesSettingButton;
                onOrganizationInfoOrLoadingChanged?.Invoke(organization, isLoading);
                return;
            }

            m_LoadOrganizationOperation.Start(token => m_AssetsProvider.GetOrganizationInfoAsync(newOrgId, token),
                onLoadingStartCallback: () =>
                {
                    errorOrMessageHandlingData.message = string.Empty;
                    errorOrMessageHandlingData.errorOrMessageRecommendedAction = ErrorOrMessageRecommendedAction.Retry;
                    onOrganizationInfoOrLoadingChanged?.Invoke(organization, isLoading);
                },
                onCancelledCallback: () => onOrganizationInfoOrLoadingChanged?.Invoke(organization, isLoading),
                onExceptionCallback: e =>
                {
                    Debug.LogException(e);
                    errorOrMessageHandlingData.message = L10n.Tr("It seems there was an error while trying to retrieve assets.");
                    errorOrMessageHandlingData.errorOrMessageRecommendedAction = ErrorOrMessageRecommendedAction.Retry;
                    onOrganizationInfoOrLoadingChanged?.Invoke(organization, isLoading);
                },
                onSuccessCallback: result =>
                {
                    m_OrganizationInfo = result;
                    if (m_OrganizationInfo?.projectInfos.Any() == true)
                    {
                        selectedProject ??= m_OrganizationInfo.projectInfos.FirstOrDefault();
                    }
                    else
                    {
                        selectedProject = null;
                        errorOrMessageHandlingData.message = k_NoProjectsMessage;
                        errorOrMessageHandlingData.errorOrMessageRecommendedAction = ErrorOrMessageRecommendedAction.OpenAssetManagerDashboardLink;
                    }
                    onOrganizationInfoOrLoadingChanged?.Invoke(organization, isLoading);
                });
        }
    }
}
