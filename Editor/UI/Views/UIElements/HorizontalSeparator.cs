using System;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
#if UNITY_6000_6_OR_NEWER
    [UxmlElement]
    partial
#endif
    class HorizontalSeparator : VisualElement
    {
        public HorizontalSeparator()
        {
            AddToClassList("horizontal-separator");
        }
#if !UNITY_6000_6_OR_NEWER
#pragma warning disable CS0618 // Type or member is obsolete
        public new class UxmlFactory : UxmlFactory<HorizontalSeparator> { }
#pragma warning restore CS0618 // Type or member is obsolete
#endif
    }
}
