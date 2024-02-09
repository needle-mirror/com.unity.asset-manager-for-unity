using System;
using Unity.AssetManager.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class TagPill : VisualElement
    {
        string m_Text;
        VisualTreeAsset m_TreeAsset;
        ClickOrDragStartManipulator m_Manipulator;

        private const string k_DarkPillClass = "pill-dark";
        private const string k_LightPillClass = "pill-light";
        
        internal Action<string> TagPillClickAction;

        public TagPill(string text)
        {
            m_Manipulator = new ClickOrDragStartManipulator(this, OnClick, null);

            m_TreeAsset = UIElementsUtils.LoadUXML(nameof(TagPill));
            m_TreeAsset.CloneTree(this);
            m_Text = text;

            var textLabel = this.Q<Label>("text");
            textLabel.text = m_Text;
            textLabel.pickingMode = PickingMode.Ignore;

            //AddToClassList(EditorGUIUtility.isProSkin ? k_DarkPillClass : k_LightPillClass);
        }

        void OnClick()
        {
            TagPillClickAction?.Invoke(m_Text);
        }
    }
}
