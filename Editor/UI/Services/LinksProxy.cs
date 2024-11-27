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
    }

    class LinksProxy : BaseService<ILinksProxy>, ILinksProxy
    {
        [SerializeReference]
        IProjectOrganizationProvider m_ProjectOrganizationProvider;

        [SerializeReference]
        IPageManager m_PageManager;

        static readonly string k_CloudStorageUpgradePlanRoute = "/products/compare-plans/unity-cloud";
        static readonly string k_HttpsUriScheme = "https://";
        static readonly string k_UnityDocsDomain = "docs.unity.com";
        static readonly string k_UnityDomain = "unity.com";

        [ServiceInjection]
        public void Inject(IProjectOrganizationProvider projectOrganizationProvider, IPageManager pageManager)
        {
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_PageManager = pageManager;
        }

        public void OpenAssetManagerDashboard()
        {
            var organizationId = m_ProjectOrganizationProvider?.SelectedOrganization?.Id;
            var projectId = m_ProjectOrganizationProvider?.SelectedProject?.Id;
            var collectionPath = m_ProjectOrganizationProvider?.SelectedCollection?.GetFullPath();
            var isProjectSelected = m_PageManager.ActivePage is CollectionPage;

            if (isProjectSelected && organizationId != null && projectId != null && !string.IsNullOrEmpty(collectionPath))
            {
                Application.OpenURL($"https://cloud.unity.com/home/organizations/{organizationId}/projects/{projectId}/assets/collectionPath/{Uri.EscapeDataString(collectionPath)}");
            }
            else if (isProjectSelected && organizationId != null && projectId != null)
            {
                Application.OpenURL($"https://cloud.unity.com/home/organizations/{organizationId}/projects/{projectId}/assets");
            }
            else if (organizationId != null && m_PageManager.ActivePage is AllAssetsPage)
            {
                Application.OpenURL($"https://cloud.unity.com/home/organizations/{organizationId}/assets/all");
            }
            else
            {
                Application.OpenURL("https://cloud.unity.com/home/");
            }

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
                Application.OpenURL($"https://cloud.unity.com/home/organizations/{m_ProjectOrganizationProvider.SelectedOrganization.Id}/projects/{projectId}/assets?assetId={assetId}:{assetVersion}");
                AnalyticsSender.SendEvent(new ExternalLinkClickedEvent(ExternalLinkClickedEvent.ExternalLinkType.OpenAsset));
            }
            else
            {
                OpenAssetManagerDashboard();
            }
        }

        public void OpenCloudStorageUpgradePlan()
        {
            Application.OpenURL($"{k_HttpsUriScheme}{k_UnityDomain}{k_CloudStorageUpgradePlanRoute}");
            AnalyticsSender.SendEvent(new ExternalLinkClickedEvent(ExternalLinkClickedEvent.ExternalLinkType.UpgradeCloudStoragePlan));
        }

        public void OpenAssetManagerDocumentationPage(string page)
        {
            Application.OpenURL($"{k_HttpsUriScheme}{k_UnityDocsDomain}/cloud/en-us/asset-manager/{page}");
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
    }
}
