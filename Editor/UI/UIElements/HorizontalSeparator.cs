using System;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class HorizontalSeparator : VisualElement
    {
        public HorizontalSeparator()
        {
            AddToClassList("horizontal-separator");
        }

        public new class UxmlFactory : UxmlFactory<HorizontalSeparator> { }
    }
}
