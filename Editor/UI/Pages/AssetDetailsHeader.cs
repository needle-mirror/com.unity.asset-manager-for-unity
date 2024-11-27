using System;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class AssetDetailsHeader : IPageComponent
    {
        readonly Label m_AssetName;
        readonly Label m_AssetVersion;
        readonly Image m_AssetDashboardLink;

        public event Action OpenDashboard;

        public AssetDetailsHeader(VisualElement visualElement)
        {
            m_AssetName = visualElement.Q<Label>("asset-name");
            m_AssetVersion = visualElement.Q<Label>("asset-version");
            m_AssetDashboardLink = visualElement.Q<Image>("asset-dashboard-link");
            m_AssetDashboardLink.tooltip = L10n.Tr(Constants.DashboardLinkTooltip);
            m_AssetDashboardLink.RegisterCallback<ClickEvent>(_ =>
            {
                OpenDashboard?.Invoke();
            });
        }

        public void OnSelection(BaseAssetData assetData)
        {
        }

        public void RefreshUI(BaseAssetData assetData, bool isLoading = false)
        {
            m_AssetName.text = assetData.Name;

            UIElementsUtils.SetSequenceNumberText(m_AssetVersion, assetData);
            UIElementsUtils.SetDisplay(m_AssetDashboardLink,
                !(string.IsNullOrEmpty(assetData.Identifier.OrganizationId) ||
                  string.IsNullOrEmpty(assetData.Identifier.ProjectId) ||
                  string.IsNullOrEmpty(assetData.Identifier.AssetId)));
        }

        public void RefreshButtons(UIEnabledStates enabled, BaseAssetData assetData, BaseOperation operationInProgress)
        {
            m_AssetDashboardLink.SetEnabled(enabled.HasFlag(UIEnabledStates.ServicesReachable));
        }
    }
}
