using System.Threading;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    static partial class UssStyle
    {
        public const string UpdateAllButtonContainer = "unity-update-all-button-container";
        public const string UpdateAllButtonIcon = "unity-update-all-button-icon";
    }

    class UpdateAllButton : GridTool
    {
        public UpdateAllButton(IAssetImporter assetImporter, IPageManager pageManager, IProjectOrganizationProvider projectOrganizationProvider)
        :base(pageManager, projectOrganizationProvider)
        {
            var updateAllButton = new Button(() =>
            {
                var project = pageManager.ActivePage is InProjectPage ? null : projectOrganizationProvider.SelectedProject;
                var collection = pageManager.ActivePage is InProjectPage ? null : projectOrganizationProvider.SelectedCollection;

                TaskUtils.TrackException(assetImporter.UpdateAllToLatestAsync(project, collection, CancellationToken.None));

                AnalyticsSender.SendEvent(new UpdateAllLatestButtonClickEvent());
            });
            Add(updateAllButton);

            var container = new VisualElement();
            container.AddToClassList(UssStyle.UpdateAllButtonContainer);
            updateAllButton.Add(container);

            var icon = new VisualElement();
            icon.AddToClassList(UssStyle.UpdateAllButtonIcon);
            container.Add(icon);

            var label = new Label(L10n.Tr(Constants.UpdateAllText));
            container.Add(label);
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
