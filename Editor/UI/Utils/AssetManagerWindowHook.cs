using System;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    class AssetManagerWindowHook
    {
        public event Action WindowEnabled;
        public event Action OrganizationLoaded;

        public void OpenAssetManagerWindow()
        {
            if (AssetManagerWindow.Instance == null)
            {
                AssetManagerWindow.Enabled += OnWindowEnabled;
                AssetManagerWindow.Open();
            }
            else
            {
                WindowEnabled?.Invoke();
                OnWindowEnabled();
            }
        }

        void OnWindowEnabled()
        {
            AssetManagerWindow.Enabled -= OnWindowEnabled;

            var provider = ServicesContainer.instance.Resolve<IProjectOrganizationProvider>();

            if (provider.SelectedOrganization == null)
            {
                provider.LoadingStateChanged += OnOrganizationLoadingStateChanged;
            }
            else
            {
                if (provider.IsLoading) 
                {
                    provider.LoadingStateChanged += OnOrganizationLoadingStateChanged;
                }
                else 
                {
                    OrganizationLoaded?.Invoke();
                }
            }
        }

        void OnOrganizationLoadingStateChanged(bool isLoading)
        {
            if (isLoading)
                return;
            
            var provider = ServicesContainer.instance.Resolve<IProjectOrganizationProvider>();
            provider.LoadingStateChanged -= OnOrganizationLoadingStateChanged;

            OrganizationLoaded?.Invoke();
        }
    }
}
