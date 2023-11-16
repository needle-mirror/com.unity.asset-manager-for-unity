using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class SideBar : VisualElement
    {
        public SideBar(IStateManager stateManager, IPageManager pageManager, IProjectOrganizationProvider projectOrganizationProvider)
        {
            var topSection = new VisualElement();
            topSection.Add(new SidebarProjectSelector(projectOrganizationProvider));
            topSection.Add(new HorizontalSeparator());
            topSection.Add(new SidebarContent(projectOrganizationProvider, pageManager, stateManager));
            Add(topSection);

            var footerContainer = new VisualElement {name = "FooterContainer"};
            footerContainer.Add(new HorizontalSeparator());
            footerContainer.AddToClassList("mb-1");
            footerContainer.Add(new SideBarButton(pageManager, string.Empty, "In Project", UIElementsUtils.GetCategoryIcon("In-Project.png"), PageType.InProject));
            Add(footerContainer);
        }
    }
}
