using System;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class DependenciesFoldout : ItemFoldout<DependencyAsset, DetailsPageDependencyItem>
    {
        readonly IPageManager m_PageManager;

        public DependenciesFoldout(VisualElement parent, string foldoutName, string listViewName,
            string loadingLabelName,
            IPageManager pageManager) : base(parent, foldoutName, listViewName, loadingLabelName)
        {
            m_PageManager = pageManager;
        }

        protected override DetailsPageDependencyItem MakeItem()
        {
            return new DetailsPageDependencyItem(m_PageManager);
        }

        protected override void BindItem(DetailsPageDependencyItem element, int index)
        {
            var dependency = (DependencyAsset)Items[index];
            _ = element.Refresh(dependency);
        }
    }
}
