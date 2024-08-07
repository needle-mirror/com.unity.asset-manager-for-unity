using System;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class TagChip : VisualElement
    {
        string m_Text;
        internal event Action<string> TagChipPointerUpAction;

        public TagChip(string text)
        {
            _ = new ClickOrDragStartManipulator(this, OnPointerUp, null, null);

            var treeAsset = UIElementsUtils.LoadUXML(nameof(TagChip));
            treeAsset.CloneTree(this);
            m_Text = text;

            var textLabel = this.Q<Label>("text");
            textLabel.text = m_Text;
            textLabel.pickingMode = PickingMode.Ignore;
        }

        void OnPointerUp(PointerUpEvent e)
        {
            TagChipPointerUpAction?.Invoke(m_Text);
        }
    }
}
