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
        ErrorHandlingData errorHandlingData { get; }
    }

    [Serializable]
    internal class ProjectOrganizationProvider : BaseService<IProjectOrganizationProvider>, IProjectOrganizationProvider
    {
        private static readonly string k_NoOrganizationMessage =
            L10n.Tr("It seems your project is not linked to an organization. Please link your project to a Unity project ID to start using the Asset Manager service.");

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
        private ErrorHandlingData m_ErrorHandling = new();
        public ErrorHandlingData errorHandlingData => m_ErrorHandling;

        private readonly IUnityConnectProxy m_UnityConnectProxy;
        private readonly IAssetsProvider m_AssetsProvider;
        public ProjectOrganizationProvider(IUnityConnectProxy unityConnectProxy, IAssetsProvider assetsProvider)
        {
            m_UnityConnectProxy = RegisterDependency(unityConnectProxy);
            m_AssetsProvider = RegisterDependency(assetsProvider);

            m_ErrorHandling.errorMessage = k_NoOrganizationMessage;
            m_ErrorHandling.errorRecommendedAction = ErrorRecommendedAction.OpenServicesSettingButton;
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

        private void OnOrganizationIdChange(string newOrgId)
        {
            var currentOrgId = m_OrganizationInfo?.id ?? string.Empty;
            newOrgId ??= string.Empty;
            if (currentOrgId == newOrgId)
                return;

            if (isLoading)
                m_LoadOrganizationOperation.Cancel();

            m_OrganizationInfo = new OrganizationInfo { id = newOrgId };
            if (string.IsNullOrEmpty(newOrgId))
            {
                m_ErrorHandling.errorMessage = k_NoOrganizationMessage;
                m_ErrorHandling.errorRecommendedAction = ErrorRecommendedAction.OpenServicesSettingButton;
                onOrganizationInfoOrLoadingChanged?.Invoke(organization, isLoading);
                return;
            }

            m_LoadOrganizationOperation.Start(token => m_AssetsProvider.GetOrganizationInfoAsync(newOrgId, token),
                onLoadingStartCallback: () =>
                {
                    errorHandlingData.errorMessage = string.Empty;
                    errorHandlingData.errorRecommendedAction = ErrorRecommendedAction.None;
                    onOrganizationInfoOrLoadingChanged?.Invoke(organization, isLoading);
                },
                onCancelledCallback: () => onOrganizationInfoOrLoadingChanged?.Invoke(organization, isLoading),
                onExceptionCallback: e =>
                {
                    Debug.LogException(e);
                    errorHandlingData.errorMessage = L10n.Tr("It seems there was an error while trying to retrieve assets.");
                    errorHandlingData.errorRecommendedAction = ErrorRecommendedAction.None;
                    onOrganizationInfoOrLoadingChanged?.Invoke(organization, isLoading);
                },
                onSuccessCallback: result =>
                {
                    m_OrganizationInfo = result;
                    selectedProject ??= m_OrganizationInfo?.projectInfos.FirstOrDefault();
                    onOrganizationInfoOrLoadingChanged?.Invoke(organization, isLoading);
                });
        }
    }
}