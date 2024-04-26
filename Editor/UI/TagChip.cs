using System;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class TagChip : VisualElement
    {
        string m_Text;
        internal event Action<string> TagChipClickAction;

        public TagChip(string text)
        {
            var manipulator = new ClickOrDragStartManipulator(this, OnClick, null);

            var treeAsset = UIElementsUtils.LoadUXML(nameof(TagChip));
            treeAsset.CloneTree(this);
            m_Text = text;

            var textLabel = this.Q<Label>("text");
            textLabel.text = m_Text;
            textLabel.pickingMode = PickingMode.Ignore;
        }

        void OnClick()
        {
            TagChipClickAction?.Invoke(m_Text);
        }
    }
}
