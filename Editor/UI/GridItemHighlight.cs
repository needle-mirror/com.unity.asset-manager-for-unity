using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class GridItemHighlight : VisualElement
    {
        const string k_UssClassName = "grid-view--item-highlight";

        internal GridItemHighlight()
        {
            pickingMode = PickingMode.Ignore;
            visible = false;
            AddToClassList(k_UssClassName);
        }
    }
}
