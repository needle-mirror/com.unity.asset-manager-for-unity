using System;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IUnityConnectProxy : IService
    {
        event Action<string> OrganizationIdChanged;
        string OrganizationId { get; }
        string ProjectId { get; }
    }

    [Serializable]
    [ExcludeFromCodeCoverage]
    class UnityConnectProxy : BaseService<IUnityConnectProxy>, IUnityConnectProxy
    {
        public event Action<string> OrganizationIdChanged;

        public string OrganizationId => m_ConnectedOrganizationId;

        public string ProjectId => m_ConnectedProjectId;

        static readonly string k_NoValue = "none";

        [SerializeField]
        string m_ConnectedOrganizationId = k_NoValue;

        [SerializeField]
        string m_ConnectedProjectId = k_NoValue;

        public override void OnEnable()
        {
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
        }
    }
}
