using System;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.UI.Editor
{
    interface ILinksProxy : IService
    {
        void OpenAssetManagerDashboard();
        void OpenAssetManagerDashboard(AssetIdentifier assetIdentifier);
        void OpenAssetManagerDocumentationPage(string page);
        void OpenProjectSettingsServices();
        void OpenPreferences();
        void OpenCloudStorageUpgradePlan();
        string GetAssetManagerDashboardUrl();
    }

    [Serializable]
    class LinksProxy : BaseService<ILinksProxy>, ILinksProxy
    {
        [SerializeReference]
        IApplicationProxy m_ApplicationProxy;

        [SerializeReference]
        IProjectOrganizationProvider m_ProjectOrganizationProvider;

        [SerializeReference]
        IPageManager m_PageManager;

        static readonly string k_CloudStorageUpgradePlanRoute = "/products/compare-plans/unity-cloud";
        static readonly string k_HttpsUriScheme = "https://";
        static readonly string k_UnityDocsDomain = "docs.unity.com";
        static readonly string k_UnityDomain = "unity.com";

        [ServiceInjection]
        public void Inject(IApplicationProxy applicationProxy, IProjectOrganizationProvider projectOrganizationProvider, IPageManager pageManager)
        {
            m_ApplicationProxy = applicationProxy;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_PageManager = pageManager;
        }

        protected override void ValidateServiceDependencies()
        {
            base.ValidateServiceDependencies();

            m_ApplicationProxy ??= ServicesContainer.instance.Get<IApplicationProxy>();
        }

        public void OpenAssetManagerDashboard()
        {
            m_ApplicationProxy.OpenUrl(GetAssetManagerDashboardUrl());

            AnalyticsSender.SendEvent(new ExternalLinkClickedEvent(ExternalLinkClickedEvent.ExternalLinkType.OpenDashboard));
            AnalyticsSender.SendEvent(new MenuItemSelectedEvent(MenuItemSelectedEvent.MenuItemType.GoToDashboard));
        }

        public void OpenAssetManagerDashboard(AssetIdentifier assetIdentifier)
        {
            var projectId = assetIdentifier?.ProjectId;
            var assetId = assetIdentifier?.AssetId;
            var assetVersion = assetIdentifier?.Version;

            if (!string.IsNullOrEmpty(projectId) && !string.IsNullOrEmpty(assetId))
            {
                m_ApplicationProxy.OpenUrl($"https://cloud.unity.com/home/organizations/{m_ProjectOrganizationProvider.SelectedOrganization.Id}/projects/{projectId}/assets?assetId={assetId}:{assetVersion}");
                AnalyticsSender.SendEvent(new ExternalLinkClickedEvent(ExternalLinkClickedEvent.ExternalLinkType.OpenAsset));
            }
            else
            {
                OpenAssetManagerDashboard();
            }
        }

        public void OpenCloudStorageUpgradePlan()
        {
            m_ApplicationProxy.OpenUrl($"{k_HttpsUriScheme}{k_UnityDomain}{k_CloudStorageUpgradePlanRoute}");
            AnalyticsSender.SendEvent(new ExternalLinkClickedEvent(ExternalLinkClickedEvent.ExternalLinkType.UpgradeCloudStoragePlan));
        }

        public void OpenAssetManagerDocumentationPage(string page)
        {
            m_ApplicationProxy.OpenUrl($"{k_HttpsUriScheme}{k_UnityDocsDomain}/cloud/en-us/asset-manager/{page}");
            AnalyticsSender.SendEvent(new MenuItemSelectedEvent(MenuItemSelectedEvent.MenuItemType.GotoSubscriptions));
        }

        public void OpenProjectSettingsServices()
        {
            SettingsService.OpenProjectSettings("Project/Services");
            AnalyticsSender.SendEvent(new MenuItemSelectedEvent(MenuItemSelectedEvent.MenuItemType.ProjectSettings));
        }

        public void OpenPreferences()
        {
            SettingsService.OpenUserPreferences("Preferences/Asset Manager");
            AnalyticsSender.SendEvent(new MenuItemSelectedEvent(MenuItemSelectedEvent.MenuItemType.Preferences));
        }

        public string GetAssetManagerDashboardUrl()
        {
            var organizationId = m_ProjectOrganizationProvider?.SelectedOrganization?.Id;
            var projectId = m_ProjectOrganizationProvider?.SelectedProject?.Id;
            var collectionPath = m_ProjectOrganizationProvider?.SelectedCollection?.GetFullPath();
            var isProjectSelected = m_PageManager.ActivePage is CollectionPage or UploadPage;

            if (isProjectSelected && organizationId != null && projectId != null && !string.IsNullOrEmpty(collectionPath))
            {
                return $"https://cloud.unity.com/home/organizations/{organizationId}/projects/{projectId}/assets/collectionPath/{Uri.EscapeDataString(collectionPath)}";
            }
            if (isProjectSelected && organizationId != null && projectId != null)
            {
                return $"https://cloud.unity.com/home/organizations/{organizationId}/projects/{projectId}/assets";
            }
            if (organizationId != null && m_PageManager.ActivePage is AllAssetsPage)
            {
                return $"https://cloud.unity.com/home/organizations/{organizationId}/assets/all";
            }

            return "https://cloud.unity.com/home/";

        }
    }
}
