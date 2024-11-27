using System;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Unity.AssetManager.Core.Editor
{
    interface IUnityConnectProxy : IService
    {
        event Action<bool> CloudServicesReachabilityChanged;
        event Action<string> OrganizationIdChanged;
        string OrganizationId { get; }
        string ProjectId { get; }

        bool HasValidOrganizationId { get; }

        bool AreCloudServicesReachable { get; }
    }

    [Serializable]
    [ExcludeFromCodeCoverage]
    class UnityConnectProxy : BaseService<IUnityConnectProxy>, IUnityConnectProxy
    {
        public event Action<bool> CloudServicesReachabilityChanged;
        public event Action<string> OrganizationIdChanged;

        public string OrganizationId => m_ConnectedOrganizationId;

        public string ProjectId => m_ConnectedProjectId;
        public bool HasValidOrganizationId => m_ConnectedOrganizationId != k_NoValue && !string.IsNullOrEmpty(m_ConnectedOrganizationId);

        public bool AreCloudServicesReachable => m_AreCloudServicesReachable != CloudServiceReachability.NotReachable;

        static readonly string k_NoValue = "none";
        static readonly string k_CloudServiceHealthCheckUrl = "https://services.api.unity.com";

        [SerializeField]
        string m_ConnectedOrganizationId = k_NoValue;

        [SerializeField]
        string m_ConnectedProjectId = k_NoValue;

        [SerializeField]
        CloudServiceReachability m_AreCloudServicesReachable = CloudServiceReachability.Unknown;

        enum CloudServiceReachability
        {
            Unknown,
            Reachable,
            NotReachable
        }

        [SerializeField]
        double m_LastInternetCheck;

        [SerializeField]
        bool m_IsInternetReachable;

        [SerializeField]
        bool m_IsCouldServicesReachableRequestComplete;

        public override void OnEnable()
        {
            m_LastInternetCheck = EditorApplication.timeSinceStartup;
            CheckCloudServicesHealth();

            EditorApplication.update += Update;
        }

        public override void OnDisable()
        {
            EditorApplication.update -= Update;
        }

        void OnProjectStateChanged()
        {
            OrganizationIdChanged?.Invoke(m_ConnectedOrganizationId);
        }

        void Update()
        {
 #if UNITY_2021
            if (CloudProjectSettings.organizationId != k_NoValue && !m_ConnectedOrganizationId.Equals(CloudProjectSettings.organizationId))
            {
                m_ConnectedOrganizationId = CloudProjectSettings.organizationId;
 #else
            if (!m_ConnectedOrganizationId.Equals(CloudProjectSettings.organizationKey))
            {
                m_ConnectedOrganizationId = CloudProjectSettings.organizationKey;
#endif
                m_ConnectedProjectId = k_NoValue;
                OnProjectStateChanged();
            }
            else if (!m_ConnectedProjectId.Equals(CloudProjectSettings.projectId))
            {
                m_ConnectedProjectId = CloudProjectSettings.projectId;
                OnProjectStateChanged();
            }

            if (EditorApplication.timeSinceStartup - m_LastInternetCheck < 2.0 || !m_IsCouldServicesReachableRequestComplete)
                return;

            m_LastInternetCheck = EditorApplication.timeSinceStartup;

            CheckCloudServicesReachability();
        }

        void CheckCloudServicesReachability()
        {
            var internetReachable = Application.internetReachability != NetworkReachability.NotReachable;
            if (m_IsInternetReachable && !internetReachable)
            {
                m_IsInternetReachable = false;
                m_AreCloudServicesReachable = CloudServiceReachability.NotReachable;
                CloudServicesReachabilityChanged?.Invoke(AreCloudServicesReachable);
            }
            else if (internetReachable)
            {
                if (!m_IsInternetReachable)
                {
                    m_IsInternetReachable = true;
                    CheckCloudServicesHealth();
                }
                else if (!AreCloudServicesReachable)
                {
                    CheckCloudServicesHealth();
                }
            }
        }

        void CheckCloudServicesHealth()
        {
            m_IsCouldServicesReachableRequestComplete = false;
            var request = UnityWebRequest.Head(k_CloudServiceHealthCheckUrl);
            var asyncOperation = request.SendWebRequest();
            try
            {
                asyncOperation.completed += _ =>
                {
                    m_AreCloudServicesReachable = request.responseCode is >= 200 and < 300 ? CloudServiceReachability.Reachable : CloudServiceReachability.NotReachable;
                    CloudServicesReachabilityChanged?.Invoke(AreCloudServicesReachable);
                    m_IsCouldServicesReachableRequestComplete = true;
                };
            }
            catch (Exception)
            {
                m_AreCloudServicesReachable = CloudServiceReachability.NotReachable;
                CloudServicesReachabilityChanged?.Invoke(AreCloudServicesReachable);
                m_IsCouldServicesReachableRequestComplete = true;
            }
        }
    }
}
