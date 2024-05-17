using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class MultiSelectionFoldout : ItemFoldout<IAssetData, MultiSelectionItem>
    {
        protected const string k_CheckMarkName = "unity-checkmark";
        
        List<IAssetData> m_FilesList = new();
        readonly Foldout m_Foldout;
        readonly Toggle m_Toggle;
        VisualElement m_CheckMark;
        Label m_Label;

        public MultiSelectionFoldout(VisualElement parent, string foldoutName, string listViewName, string buttonTitle, Action buttonCallback , string foldoutTitle = null, string foldoutExpandedClassName = null)
            : base(parent, foldoutName, listViewName, foldoutTitle, foldoutExpandedClassName)
        {
            m_Foldout = parent.Q<Foldout>(foldoutName);
            m_Toggle = m_Foldout.Q<Toggle>();
            m_CheckMark = m_Toggle.Q<VisualElement>(k_CheckMarkName);
            m_CheckMark.parent.style.flexDirection = FlexDirection.Row;
            m_Label = m_Toggle.Q<Label>();
            m_Label.style.position = Position.Relative;
            var button = new Button
            {
                text = buttonTitle
            };
            button.clicked += buttonCallback;
            button.style.position = Position.Relative;
            button.style.paddingLeft = 6;
            m_Toggle.Add(button);
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
