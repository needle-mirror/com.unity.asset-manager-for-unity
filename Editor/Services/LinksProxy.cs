using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal interface ILinksProxy : IService
    {
        void OpenAssetManagerDashboard();
        void OpenProjectSettingsServices();
        void OpenPreferences();
    }
    
    internal class LinksProxy : BaseService<ILinksProxy>, ILinksProxy
    {

        private readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        public LinksProxy(IProjectOrganizationProvider projectOrganizationProvider)
        {
            m_ProjectOrganizationProvider = projectOrganizationProvider;
        }
        
        public void OpenAssetManagerDashboard()
        {
            if (m_ProjectOrganizationProvider?.organization?.id != null)
                    Application.OpenURL("https://cloud.unity.com/home/organizations/" + m_ProjectOrganizationProvider.organization.id + "/assets/all");
            else
                Application.OpenURL("https://cloud.unity.com/home/");
        }

        public void OpenProjectSettingsServices()
        {
            SettingsService.OpenProjectSettings("Project/Services");
        }
        
        public void OpenPreferences()
        {
            SettingsService.OpenUserPreferences("Preferences/Asset Manager");
        }
    }
}
