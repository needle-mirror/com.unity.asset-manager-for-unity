using System;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal interface ILinksProxy : IService
    {
        void OpenAssetManagerDashboard();
        void OpenAssetManagerDashboard(AssetIdentifier assetIdentifier);
        void OpenProjectSettingsServices();
        void OpenPreferences();
    }

    internal class LinksProxy : BaseService<ILinksProxy>, ILinksProxy
    {
        [SerializeReference]
        IProjectOrganizationProvider m_ProjectOrganizationProvider;

        [ServiceInjection]
        public void Inject(IProjectOrganizationProvider projectOrganizationProvider)
        {
            m_ProjectOrganizationProvider = projectOrganizationProvider;
        }

        public void OpenAssetManagerDashboard()
        {
            var organizationId = m_ProjectOrganizationProvider?.SelectedOrganization?.id;
            var projectId = m_ProjectOrganizationProvider?.SelectedProject?.id;
            var collectionPath = m_ProjectOrganizationProvider?.SelectedCollection?.GetFullPath();

            if (organizationId != null && projectId != null && !string.IsNullOrEmpty(collectionPath))
            {
                Application.OpenURL($"https://cloud.unity.com/home/organizations/{organizationId}/projects/{projectId}/assets/collectionPath/{Uri.EscapeDataString(collectionPath)}");
            }
            else if (organizationId != null && projectId != null)
            {
                Application.OpenURL($"https://cloud.unity.com/home/organizations/{organizationId}/projects/{projectId}/assets");
            }
            else if (organizationId != null)
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
            var projectId = assetIdentifier?.projectId;
            var assetId = assetIdentifier?.assetId;
            var assetVersion = assetIdentifier?.version;

            if(!string.IsNullOrEmpty(projectId) && !string.IsNullOrEmpty(assetId))
            {
                Application.OpenURL($"https://cloud.unity.com/home/organizations/{m_ProjectOrganizationProvider.SelectedOrganization.id}/projects/{projectId}/assets?assetId={assetId}:{assetVersion}");
                AnalyticsSender.SendEvent(new ExternalLinkClickedEvent(ExternalLinkClickedEvent.ExternalLinkType.OpenAsset));
            }
            else
            {
                OpenAssetManagerDashboard();
            }
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
