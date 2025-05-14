using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace Unity.AssetManager.UI.Editor
{
    static partial class UssStyle
    {
        public const string UpdateAllButtonContainer = "unity-update-all-button-container";
        public const string UpdateAllButtonIcon = "unity-update-all-button-icon";
    }

    class UpdateAllButton : GridTool
    {
        private readonly Button m_UpdateAllButton;
        private readonly VisualElement m_Icon;
        private readonly IAssetDataManager m_AssetDataManager;
        private readonly HashSet<BaseAssetData> m_TrackedAssets = new();

        public UpdateAllButton(IAssetImporter assetImporter, IPageManager pageManager, IProjectOrganizationProvider projectOrganizationProvider)
        :base(pageManager, projectOrganizationProvider)
        {
            m_UpdateAllButton = new Button(() =>
            {
                var project = pageManager.ActivePage is InProjectPage ? null : projectOrganizationProvider.SelectedProject;
                var collection = pageManager.ActivePage is InProjectPage ? null : projectOrganizationProvider.SelectedCollection;

                TaskUtils.TrackException(assetImporter.UpdateAllToLatestAsync(ImportTrigger.UpdateAllToLatest, project, collection, CancellationToken.None));

                AnalyticsSender.SendEvent(new UpdateAllLatestButtonClickEvent());
            });
            Add(m_UpdateAllButton);

            var container = new VisualElement();
            container.AddToClassList(UssStyle.UpdateAllButtonContainer);
            m_UpdateAllButton.Add(container);

            m_Icon = new VisualElement();
            m_Icon.AddToClassList(UssStyle.UpdateAllButtonIcon);

            m_AssetDataManager = ServicesContainer.instance.Resolve<IAssetDataManager>();

            m_AssetDataManager.AssetDataChanged += OnAssetDataChanged;
            m_AssetDataManager.ImportedAssetInfoChanged += OnAssetDataChanged;

            UpdateButtonState(false);

            container.Add(m_Icon);
        }

        private async void OnAssetDataChanged(AssetChangeArgs obj)
        {
            // Wait for the UI to update before processing the asset data changes
            await Task.Yield();

            // Unsubscribe from old assets
            foreach (var asset in m_TrackedAssets)
            {
                asset.AssetDataChanged -= OnAssetDataAttributesChanged;
            }
            m_TrackedAssets.Clear();

            // Get current page assets
            var currentPage = m_PageManager.ActivePage;
            if (currentPage == null)
                return;

            var pageAssets = currentPage.AssetList
                .ToList();

            if (!pageAssets.Any())
            {
                UpdateButtonState(false);
                return;
            }

            // Subscribe to new assets
            foreach (var asset in pageAssets)
            {
                asset.AssetDataChanged += OnAssetDataAttributesChanged;
                m_TrackedAssets.Add(asset);
            }

            CheckAssetsStatus(pageAssets);
        }

        private void OnAssetDataAttributesChanged(BaseAssetData asset, AssetDataEventType changeType)
        {
            if (changeType == AssetDataEventType.AssetDataAttributesChanged)
            {
                CheckAssetsStatus(m_TrackedAssets.ToList());
            }
        }

        private void CheckAssetsStatus(List<BaseAssetData> assets)
        {
            var hasOutdatedAssets = assets.Any(asset =>
            {
                var importAttribute = asset.AssetDataAttributeCollection?.GetAttribute<ImportAttribute>();
                return importAttribute?.Status == ImportAttribute.ImportStatus.OutOfDate;
            });

            UpdateButtonState(hasOutdatedAssets);
        }

        ~UpdateAllButton()
        {
            foreach (var asset in m_TrackedAssets)
            {
                asset.AssetDataChanged -= OnAssetDataAttributesChanged;
            }
            m_TrackedAssets.Clear();

            m_AssetDataManager.AssetDataChanged -= OnAssetDataChanged;
            m_AssetDataManager.ImportedAssetInfoChanged -= OnAssetDataChanged;
        }

        public void UpdateButtonState(bool hasOutdatedAssets)
        {
            m_Icon.RemoveFromClassList(hasOutdatedAssets ? "inactive" : "active");
            m_Icon.AddToClassList(hasOutdatedAssets ? "active" : "inactive");
            m_UpdateAllButton.SetEnabled(hasOutdatedAssets);
            m_UpdateAllButton.visible = m_PageManager.ActivePage is not AllAssetsPage;
        }

        protected override bool IsDisplayed(IPage page)
        {
            if (page is BasePage basePage)
            {
                return basePage.DisplayUpdateAllButton;
            }

            return base.IsDisplayed(page);
        }
    }
}
