using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class SideBar : VisualElement
    {
        public SideBar(IStateManager stateManager, IPageManager pageManager, IProjectOrganizationProvider projectOrganizationProvider)
        {
            var topSection = new VisualElement();

            var title = new Label("Projects");
            title.AddToClassList("SidebarTitle");
            topSection.Add(title);
            topSection.Add(new SidebarContent(projectOrganizationProvider, pageManager, stateManager));
            Add(topSection);

            var footerContainer = new VisualElement {name = "FooterContainer"};
            footerContainer.Add(new HorizontalSeparator());
            footerContainer.AddToClassList("mb-1");
            footerContainer.Add(new SideBarButton<InProjectPage>(pageManager, "In Project", UIElementsUtils.GetCategoryIcon("In-Project.png")));
            Add(footerContainer);
        }
    }
}
