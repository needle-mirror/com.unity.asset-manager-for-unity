using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class MultiSelectionFoldout : ItemFoldout<IAssetData, MultiSelectionItem>
    {
        List<IAssetData> m_FilesList = new();

        public MultiSelectionFoldout(VisualElement parent, string foldoutName, string listViewName, string loadingLabelName, string foldoutTitle = null, string foldoutExpandedClassName = null)
            : base(parent, foldoutName, listViewName, loadingLabelName, foldoutTitle, foldoutExpandedClassName)
        {
        }

        public override void Clear()
        {
            base.Clear();
            m_FilesList.Clear();
        }

        protected override IList PrepareListItem(IAssetData assetData, IEnumerable<IAssetData> items)
        {
            m_FilesList = new List<IAssetData>();

            foreach (var assetDataFile in items.OrderBy(f => f.Name))
            {
                m_FilesList.Add(assetDataFile);
            }

            return m_FilesList;
        }

        protected override MultiSelectionItem MakeItem()
        {
            return new MultiSelectionItem();
        }

        protected override void BindItem(MultiSelectionItem element, int index)
        {
            var fileItem = m_FilesList[index];
            element.Refresh(fileItem);
        }
    }
}
