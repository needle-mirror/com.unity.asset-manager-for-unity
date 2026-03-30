using Unity.AssetManager.Core.Editor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    static partial class UssStyle
    {
        public const string AssetManagerFooter = "asset-manager-footer";
        public const string AssetManagerFooterLabel = "asset-manager-footer-label";
        public const string AssetManagerFooterRefreshButton = "asset-manager-footer-refresh-button";
    }

    class AssetManagerFooter : VisualElement
    {
        readonly Label m_LastSyncLabel;
        readonly Button m_RefreshButton;
        readonly IAssetDataCacheSyncService m_SyncService;
        readonly IPageManager m_PageManager;
        readonly VisualElement m_SyncContainer;
        readonly IVisualElementScheduledItem m_ScheduledUpdate;

        public AssetManagerFooter(IPageManager pageManager, IAssetDataCacheSyncService syncService)
        {
            m_PageManager = pageManager;
            m_SyncService = syncService;

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            AddToClassList(UssStyle.AssetManagerFooter);

            m_SyncContainer = new VisualElement();
            m_SyncContainer.style.flexDirection = FlexDirection.Row;
            m_SyncContainer.style.alignItems = Align.Center;
            Add(m_SyncContainer);

            m_LastSyncLabel = new Label();
            m_LastSyncLabel.AddToClassList(UssStyle.AssetManagerFooterLabel);
            m_SyncContainer.Add(m_LastSyncLabel);

            m_RefreshButton = new Button(OnRefreshClicked)
            {
                text = Constants.RefreshNowButtonText,
                tooltip = Constants.RefreshNowButtonTooltip
            };
            m_RefreshButton.AddToClassList(UssStyle.AssetManagerFooterRefreshButton);
            m_SyncContainer.Add(m_RefreshButton);

            // Update label every minute
            m_ScheduledUpdate = schedule.Execute(UpdateLastSyncDisplay).Every(60000);
            UpdateSyncVisibility(m_PageManager.ActivePage);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_PageManager.ActivePageChanged += OnActivePageChanged;
            m_ScheduledUpdate.Resume();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_PageManager.ActivePageChanged -= OnActivePageChanged;
            m_ScheduledUpdate.Pause();
        }

        void OnActivePageChanged(IPage page)
        {
            UpdateSyncVisibility(page);
        }

        void UpdateSyncVisibility(IPage page)
        {
            var showSync = page is InProjectPage or UploadPage;
            m_SyncContainer.style.display = showSync ? DisplayStyle.Flex : DisplayStyle.None;

            if (showSync)
                UpdateLastSyncDisplay();
        }

        void OnRefreshClicked()
        {
            m_SyncService.Sync();
            UpdateLastSyncDisplay();
        }

        void UpdateLastSyncDisplay()
        {
            if (m_SyncService.LastSyncTime.HasValue)
            {
                var relativeTime = Utilities.FormatRelativeTime(m_SyncService.LastSyncTime.Value);
                m_LastSyncLabel.text = Constants.LastSyncPrefix + relativeTime;
            }
            else
            {
                m_LastSyncLabel.text = Constants.LastSyncPrefix + Constants.LastSyncNever;
            }
        }
    }
}
