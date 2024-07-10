using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    static partial class UssStyle
    {
        public const string AssetVersion_Draft = "asset-version--draft";
        public const string AssetVersion_InReview = "asset-version--in-review";
        public const string AssetVersion_Approved = "asset-version--approved";
        public const string AssetVersion_Rejected = "asset-version--rejected";
        public const string AssetVersion_Published = "asset-version--published";
        public const string AssetVersion_Withdrawn = "asset-version--withdrawn";
    }
    
    class AssetDetailsHeader : IPageComponent
    {
        readonly Label m_AssetName;
        readonly Label m_AssetVersion;
        readonly VisualElement m_AssetStatusChip;
        readonly Image m_AssetDashboardLink;
        
        string m_CurrentStatusChipStyle;

        public event Action OpenDashboard;

        public AssetDetailsHeader(VisualElement visualElement)
        {
            m_AssetName = visualElement.Q<Label>("asset-name");
            m_AssetVersion = visualElement.Q<Label>("asset-version");
            m_AssetStatusChip = visualElement.Q<VisualElement>("asset-status-chip");
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

            switch (assetData.Status)
            {
                case Constants.AssetDraftStatus:
                    ChangeStatusChipColor(UssStyle.AssetVersion_Draft);
                    break;
                case Constants.AssetInReviewStatus:
                    ChangeStatusChipColor(UssStyle.AssetVersion_InReview);
                    break;
                case Constants.AssetApprovedStatus:
                    ChangeStatusChipColor(UssStyle.AssetVersion_Approved);
                    break;
                case Constants.AssetRejectedStatus:
                    ChangeStatusChipColor(UssStyle.AssetVersion_Rejected);
                    break;
                case Constants.AssetPublishedStatus:
                    ChangeStatusChipColor(UssStyle.AssetVersion_Published);
                    break;
                case Constants.AssetWithdrawnStatus:
                    ChangeStatusChipColor(UssStyle.AssetVersion_Withdrawn);
                    break;
            }

            UIElementsUtils.SetDisplay(m_AssetDashboardLink,
                !(string.IsNullOrEmpty(assetData.Identifier.OrganizationId) ||
                    string.IsNullOrEmpty(assetData.Identifier.ProjectId) ||
                    string.IsNullOrEmpty(assetData.Identifier.AssetId)));
        }

        void ChangeStatusChipColor(string newStyle)
        {
            if (newStyle == m_CurrentStatusChipStyle)
                return;
            
            if(!string.IsNullOrEmpty(m_CurrentStatusChipStyle))
                m_AssetStatusChip.RemoveFromClassList(m_CurrentStatusChipStyle);
                    
            m_AssetStatusChip.AddToClassList(newStyle);
            m_CurrentStatusChipStyle = newStyle;
        }

        public void RefreshButtons(UIEnabledStates enabled, IAssetData assetData, BaseOperation operationInProgress)
        {
            m_AssetDashboardLink.SetEnabled(enabled.HasFlag(UIEnabledStates.ServicesReachable));
        }
    }
}
