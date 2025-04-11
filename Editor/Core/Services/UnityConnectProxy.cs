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
        event Action OrganizationIdChanged;
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
        public event Action OrganizationIdChanged;

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

        [SerializeReference]
        IApplicationProxy m_ApplicationProxy;

        [SerializeField]
        double m_LastInternetCheck;

        [SerializeField]
        bool m_IsCouldServicesReachableRequestComplete;

        [ServiceInjection]
        public void Inject(IApplicationProxy applicationProxy)
        {
            m_ApplicationProxy = applicationProxy;
        }

        public override void OnEnable()
        {
            base.OnEnable();

            m_LastInternetCheck = m_ApplicationProxy.TimeSinceStartup;
            CheckCloudServicesHealth();

            m_ApplicationProxy.Update += Update;
        }

        protected override void ValidateServiceDependencies()
        {
            base.ValidateServiceDependencies();

            m_ApplicationProxy ??= ServicesContainer.instance.Get<IApplicationProxy>();
        }

        public override void OnDisable()
        {
            if (m_ApplicationProxy != null)
                m_ApplicationProxy.Update -= Update;
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
                OrganizationIdChanged?.Invoke();
            }
            else if (!m_ConnectedProjectId.Equals(CloudProjectSettings.projectId))
            {
                m_ConnectedProjectId = CloudProjectSettings.projectId;
            }

            var timeSinceStartup = m_ApplicationProxy.TimeSinceStartup;

            if (timeSinceStartup - m_LastInternetCheck < 2.0 || !m_IsCouldServicesReachableRequestComplete)
                return;

            m_LastInternetCheck = timeSinceStartup;

            CheckCloudServicesReachability();
        }

        void CheckCloudServicesReachability()
        {
            if (!m_ApplicationProxy.InternetReachable && m_AreCloudServicesReachable != CloudServiceReachability.NotReachable)
            {
                m_AreCloudServicesReachable = CloudServiceReachability.NotReachable;
                CloudServicesReachabilityChanged?.Invoke(AreCloudServicesReachable);
            }
            else
            {
                CheckCloudServicesHealth();
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
                    var cloudServiceReachability = request.result == UnityWebRequest.Result.Success
                        ? CloudServiceReachability.Reachable
                        : CloudServiceReachability.NotReachable;

                    if (m_AreCloudServicesReachable != cloudServiceReachability)
                    {
                        m_AreCloudServicesReachable = cloudServiceReachability;
                        CloudServicesReachabilityChanged?.Invoke(AreCloudServicesReachable);
                    }
                };
            }
            catch (Exception)
            {
                if (AreCloudServicesReachable)
                {
                    m_AreCloudServicesReachable = CloudServiceReachability.NotReachable;
                    CloudServicesReachabilityChanged?.Invoke(AreCloudServicesReachable);
                }
            }
            finally
            {
                m_IsCouldServicesReachableRequestComplete = true;
            }
        }
    }
}
