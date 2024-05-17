using System;
using System.Threading.Tasks;
using Unity.Cloud.Common;
using Unity.Cloud.Identity;
using Unity.Cloud.Identity.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    interface IStorageInfoHelpBox
    {
        void RefreshCloudStorageAsync(TimerState timerState);
    }

    class StorageInfoHelpBox : HelpBox, IStorageInfoHelpBox
    {
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IAssetsProvider m_AssetsProvider;

        readonly IUnityConnectProxy m_UnityConnectProxy;

        ICloudStorageUsage m_CloudStorageUsage;
        IOrganization m_Organization;

        static readonly string k_StorageUsageWarningMessage = L10n.Tr("Your organization has used {0}% of your included Unity Asset Manager cloud storage.");
        static readonly string k_ContactOrganizationOwnerMessage = L10n.Tr("Contact your organization owner to upgrade your plan.");
        static readonly string k_UpgradeToContinueMessage = L10n.Tr("Upgrade to continue use without interruption.");
        static readonly string k_Upgrade = L10n.Tr("Upgrade");

        static readonly int? k_StoragePercentUsageWarningThreshold = 75;

        public StorageInfoHelpBox(IProjectOrganizationProvider projectOrganizationProvider,
            ILinksProxy linksProxy, IAssetsProvider assetsProvider, IUnityConnectProxy unityConnectProxy)
        {
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_AssetsProvider = assetsProvider;
            m_UnityConnectProxy = unityConnectProxy;

            messageType = HelpBoxMessageType.Info;

            var cloudStorageUpgradeButton = new Button(linksProxy.OpenCloudStorageUpgradePlan)
            {
                text = k_Upgrade
            };
            Add(cloudStorageUpgradeButton);

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_ProjectOrganizationProvider.OrganizationChanged += OnOrganizationChanged;
            OnOrganizationChanged(m_ProjectOrganizationProvider.SelectedOrganization);
            Refresh();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_ProjectOrganizationProvider.OrganizationChanged -= OnOrganizationChanged;
        }

        public async void RefreshCloudStorageAsync(TimerState timerState)
        {
            m_CloudStorageUsage = await GetCloudStorageUsageAsync();
            Refresh();
        }

        async Task<ICloudStorageUsage> GetCloudStorageUsageAsync()
        {
            if (m_Organization == null || !m_UnityConnectProxy.AreCloudServicesReachable || string.IsNullOrEmpty(m_ProjectOrganizationProvider.SelectedOrganization?.Id))
                return null;

            return await m_AssetsProvider.GetOrganizationCloudStorageUsageAsync(m_Organization);
        }

        void Refresh()
        {
            if (m_CloudStorageUsage == null)
            {
                UIElementsUtils.Hide(this);
                return;
            }

            var percentUsage = FormatUsage(m_CloudStorageUsage.UsageBytes * 1.0 / m_CloudStorageUsage.TotalStorageQuotaBytes * 1.0);
            if (percentUsage < k_StoragePercentUsageWarningThreshold)
            {
                UIElementsUtils.Hide(this);
                return;
            }

            var callToActionText = UserCanUpgradeStoragePlan() ? k_UpgradeToContinueMessage : k_ContactOrganizationOwnerMessage;
            text = $"{string.Format(k_StorageUsageWarningMessage, percentUsage)} {callToActionText}";
            UIElementsUtils.Show(this);
        }

        int FormatUsage(double usage)
        {
            return Convert.ToInt32(Math.Round(usage * 100.0));
        }

        bool UserCanUpgradeStoragePlan()
        {
            if (m_Organization == null)
                return false;

            return m_Organization.Role.Equals(Unity.Cloud.Common.Role.Manager) ||
                   m_Organization.Role.Equals(Unity.Cloud.Common.Role.Owner);
        }

        async void OnOrganizationChanged(OrganizationInfo organizationInfo)
        {
            m_CloudStorageUsage = null;
            m_Organization = null;
            if (!string.IsNullOrEmpty(organizationInfo?.Id))
            {
                m_Organization = await m_AssetsProvider.GetOrganizationAsync(organizationInfo.Id);
            }

            m_CloudStorageUsage = await GetCloudStorageUsageAsync();
            Refresh();
        }
    }
}
