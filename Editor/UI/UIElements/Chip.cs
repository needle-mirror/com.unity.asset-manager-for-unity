using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class Chip : VisualElement
    {
        protected Label m_Label;

        public Chip(string text)
        {

            m_Label = new Label(text)
            {
                pickingMode = PickingMode.Ignore
            };

            Add(m_Label);
        }
    }
}
