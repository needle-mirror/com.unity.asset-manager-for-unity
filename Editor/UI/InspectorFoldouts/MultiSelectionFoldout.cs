using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class MultiSelectionFoldout : ItemFoldout<IAssetData, MultiSelectionItem>
    {
        protected const string k_CheckMarkName = "unity-checkmark";

        List<IAssetData> m_FilesList = new();
        readonly Button m_Button;

        public MultiSelectionFoldout(VisualElement parent, string foldoutName, string listViewName, string buttonTitle, Action buttonCallback , string foldoutTitle = null, string foldoutExpandedClassName = null)
            : base(parent, foldoutName, listViewName, foldoutTitle, foldoutExpandedClassName)
        {
            var foldout = parent.Q<Foldout>(foldoutName);
            var toggle = foldout.Q<Toggle>();
            var checkmark = toggle.Q<VisualElement>(k_CheckMarkName);
            checkmark.parent.style.flexDirection = FlexDirection.Row;
            var label = toggle.Q<Label>();
            label.style.position = Position.Relative;
            m_Button = new Button
            {
                text = L10n.Tr(buttonTitle)
            };
            m_Button.clicked += buttonCallback;
            m_Button.style.position = Position.Relative;
            m_Button.style.paddingLeft = 6;
            toggle.Add(m_Button);
        }

        public override void Clear()
        {
            base.Clear();
            m_FilesList.Clear();
        }

        public override void RemoveItems(IEnumerable<IAssetData> items)
        {
            var list = items.ToList();
            base.RemoveItems(list);

            foreach (var item in list)
            {
                m_FilesList.Remove(item);
            }
        }

        public void SetButtonDisplayed(bool displayed)
        {
            UIElementsUtils.SetDisplay(m_Button, displayed);
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
