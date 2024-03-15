using System;
using UnityEditor.Connect;

namespace Unity.AssetManager.Editor
{
    internal class UnityConnectSession
    {
        public event Action onUserStateChanged;
        public event Action onProjectStateChanged;

        public void OnEnable()
        {
            UnityConnect.instance.UserStateChanged += OnUserStateChanged;
            UnityConnect.instance.ProjectStateChanged += OnProjectStateChanged;
        }

        public void OnDisable()
        {
            UnityConnect.instance.UserStateChanged -= OnUserStateChanged;
            UnityConnect.instance.ProjectStateChanged -= OnProjectStateChanged;
        }

        private void OnUserStateChanged(UserInfo userInfo)
        {
            onUserStateChanged?.Invoke();
        }

        private void OnProjectStateChanged(ProjectInfo projectInfo)
        {
            onProjectStateChanged?.Invoke();
        }

        public bool isUserInfoReady => UnityConnect.instance.isUserInfoReady;

        // Note that the `OrganizationForeignKey` in UnityConnect matches with `OrganizationId` in AssetSDK
        // We rename the function here so it's more consistent in the rest of the code.
        public string GetOrganizationId() => UnityConnect.instance.GetOrganizationForeignKey();
        
        public string GetProjectId() => UnityConnect.instance.GetProjectGUID();

        public string GetAccessToken() => UnityConnect.instance.GetAccessToken();

        public string GetUserID() => UnityConnect.instance.GetUserId();

        public void ShowLogin() => UnityConnect.instance.ShowLogin();

        public void OpenAuthorizedURLInWebBrowser(string url) => UnityConnect.instance.OpenAuthorizedURLInWebBrowser(url);
    }
}
