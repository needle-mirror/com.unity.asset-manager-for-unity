using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    static partial class UssStyle
    {
        public const string UpwardDependencyItem = "upward-dependency-item";
        public const string UpwardDependencyItemAssetName = UpwardDependencyItem + "-asset-name";
    }

    class UpwardDependencyItem : VisualElement
    {
        internal UpwardDependencyItem(IAssetData assetData)
        {
            var assetName = new Label(assetData.Name);
            assetName.AddToClassList(UssStyle.UpwardDependencyItemAssetName);
            Add(assetName);
        }
    }
}
