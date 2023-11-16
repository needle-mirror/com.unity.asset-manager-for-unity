using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class HorizontalSeparator : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<HorizontalSeparator> { }

        public HorizontalSeparator()
        {
            AddToClassList("horizontal-separator");
        }
    }
}
