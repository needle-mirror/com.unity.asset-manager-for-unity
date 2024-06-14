using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    static partial class UssStyle
    {
        public const string AssetVersion_Draft = "asset-version--draft";
    }
    
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

        public void OnSelection(IAssetData assetData, bool isLoading) { }

        public void RefreshUI(IAssetData assetData, bool isLoading = false)
        {
            m_AssetName.text = assetData.Name;
            m_AssetVersion.text = assetData.SequenceNumber > 0
                ? L10n.Tr(Constants.VersionText) + assetData.SequenceNumber
                : L10n.Tr(Constants.PendingVersionText);
            m_AssetVersion.tooltip = assetData.Identifier.Version;

            if (assetData.Status == Constants.AssetDraftStatus)
            {
                m_AssetVersion.AddToClassList(UssStyle.AssetVersion_Draft);
            }
            else
            {
                m_AssetVersion.RemoveFromClassList(UssStyle.AssetVersion_Draft);
            }

            UIElementsUtils.SetDisplay(m_AssetDashboardLink,
                !(string.IsNullOrEmpty(assetData.Identifier.OrganizationId) ||
                    string.IsNullOrEmpty(assetData.Identifier.ProjectId) ||
                    string.IsNullOrEmpty(assetData.Identifier.AssetId)));
        }

        public void RefreshButtons(UIEnabledStates enabled, IAssetData assetData, BaseOperation operationInProgress)
        {
            m_AssetDashboardLink.SetEnabled(enabled.HasFlag(UIEnabledStates.ServicesReachable));
        }
    }
}
