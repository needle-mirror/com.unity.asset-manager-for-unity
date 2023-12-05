using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class AssetPreview : VisualElement
    {
        private const string k_ThumbnailUssClassName = "asset-preview-thumbnail";
        private const string k_AssetPreviewUssClassName = "asset-preview";
        private const string k_AssetTypeIconUssClassName = "asset-preview-asset-type-icon";
        private const string k_DefaultAssetIconUssClassName = "default-asset-icon";
        
        private DraggableImage m_DraggableImage;
        private VisualElement m_AssetTypeIcon;

        private readonly IIconFactory m_IconFactory;
        public AssetPreview(IIconFactory iconFactory)
        {
            m_IconFactory = iconFactory;
            
            m_DraggableImage = new DraggableImage();
            m_AssetTypeIcon = new VisualElement();
            
            AddToClassList(k_AssetPreviewUssClassName);
            m_DraggableImage.AddToClassList(k_ThumbnailUssClassName);
            m_AssetTypeIcon.AddToClassList(k_AssetTypeIconUssClassName);

            Add(m_DraggableImage);
            Add(m_AssetTypeIcon);
        }
        
        public void SetAssetType(AssetType assetType, bool useAssetTypeAsTooltip)
        {
            var icon = m_IconFactory.GetIconByType(assetType);
            m_AssetTypeIcon.EnableInClassList(k_DefaultAssetIconUssClassName, icon == null);
            m_AssetTypeIcon.style.backgroundImage = icon == null ? StyleKeyword.Null : icon;
            m_AssetTypeIcon.tooltip = useAssetTypeAsTooltip ? assetType.DisplayValue() : string.Empty;
        }

        public void SetThumbnail(Texture2D texture2D)
        {
            EnableInClassList("no-thumbnail", texture2D == null);
            m_DraggableImage.SetBackgroundImage(texture2D);
        }
    }
}
