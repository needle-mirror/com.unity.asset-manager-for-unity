using System;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class DependenciesFoldout : ItemFoldout<AssetIdentifier, DetailsPageDependencyItem>
    {
        readonly IPageManager m_PageManager;

        public DependenciesFoldout(VisualElement parent, string foldoutName, string listViewName,
            IPageManager pageManager, string foldoutTitle = null)
            : base(parent, foldoutName, listViewName, foldoutTitle)
        {
            m_PageManager = pageManager;
        }

        protected override DetailsPageDependencyItem MakeItem()
        {
            return new DetailsPageDependencyItem(m_PageManager);
        }

        protected override void BindItem(DetailsPageDependencyItem element, int index)
        {
            var dependency = (AssetIdentifier)Items[index];
            TaskUtils.TrackException(element.Refresh(dependency));
        }
    }
}
