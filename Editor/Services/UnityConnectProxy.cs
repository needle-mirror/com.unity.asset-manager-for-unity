using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal interface IUnityConnectProxy : IService
    {
        event Action<bool /*isUserInfoReady*/, bool /*isUserLoggedIn*/> onUserLoginStateChange;
        event Action<string> onOrganizationIdChange;
        bool isUserLoggedIn { get; }
        string organizationId { get; }
        void ShowLogin();
    }

    [Serializable]
    [ExcludeFromCodeCoverage]
    internal class UnityConnectProxy : BaseService<IUnityConnectProxy>, IUnityConnectProxy
    {
        public event Action<bool, bool> onUserLoginStateChange;
        public event Action<string> onOrganizationIdChange;

        [SerializeField]
        private bool m_IsUserInfoReady;
        public bool isUserInfoReady => m_IsUserInfoReady;

        [SerializeField]
        private bool m_HasAccessToken;
        public bool isUserLoggedIn => m_IsUserInfoReady && m_HasAccessToken;

        [SerializeField]
        private string m_UserId = string.Empty;

        [SerializeField]
        private string m_OrganizationId;
        public string organizationId => m_OrganizationId;

        private readonly UnityConnectSession m_UnityConnectSession = new();

        public override void OnEnable()
        {
            m_UnityConnectSession.OnEnable();

            m_IsUserInfoReady = m_UnityConnectSession.isUserInfoReady;
            m_HasAccessToken = !string.IsNullOrEmpty(m_UnityConnectSession.GetAccessToken());
            m_OrganizationId = m_UnityConnectSession.GetOrganizationId();

            m_UnityConnectSession.onUserStateChanged += OnUserStateChanged;
            m_UnityConnectSession.onProjectStateChanged += OnProjectStateChanged;
        }

        public override void OnDisable()
        {
            m_UnityConnectSession.OnDisable();
            m_UnityConnectSession.onUserStateChanged -= OnUserStateChanged;
            m_UnityConnectSession.onProjectStateChanged -= OnProjectStateChanged;
        }

        public string GetAccessToken() => m_UnityConnectSession.GetAccessToken();
        public void ShowLogin() => m_UnityConnectSession.ShowLogin();
        public void OpenAuthorizedURLInWebBrowser(string url) => m_UnityConnectSession.OpenAuthorizedURLInWebBrowser(url);

        private void OnUserStateChanged()
        {
            var prevIsUserInfoReady = isUserInfoReady;
            var prevIsUserLoggedIn = isUserLoggedIn;
            var prevUserId = m_UserId;

            m_IsUserInfoReady = m_UnityConnectSession.isUserInfoReady;
            m_HasAccessToken = !string.IsNullOrEmpty(m_UnityConnectSession.GetAccessToken());
            m_UserId = m_UnityConnectSession.GetUserID();

            if (isUserInfoReady != prevIsUserInfoReady || isUserLoggedIn != prevIsUserLoggedIn || prevUserId != m_UserId)
                onUserLoginStateChange?.Invoke(isUserInfoReady, isUserLoggedIn);
        }

        private void OnProjectStateChanged()
        {
            var oldOrganizationId = m_OrganizationId ?? string.Empty;
            m_OrganizationId = m_UnityConnectSession.GetOrganizationId() ?? string.Empty;
            if (oldOrganizationId == m_OrganizationId)
                return;
            onOrganizationIdChange?.Invoke(m_OrganizationId);
        }
    }
}
