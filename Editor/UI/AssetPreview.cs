using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class AssetPreview : VisualElement
    {
        static class UssStyles
        {
            public static readonly string Thumbnail = "asset-preview-thumbnail";
            public static readonly string AssetPreview = "asset-preview";
            public static readonly string AssetTypeIcon = "asset-preview-asset-type-icon";
            public static readonly string DefaultAssetIcon = "default-asset-icon";
            public static readonly string ImportedStatus = Constants.GridItemStyleClassName + "-imported_status";
        }

        readonly DraggableImage m_DraggableImage;
        readonly VisualElement m_AssetTypeIcon;
        readonly VisualElement m_ImportedStatusIcon;

        public AssetPreview()
        {
            m_DraggableImage = new DraggableImage();
            m_AssetTypeIcon = new VisualElement();

            AddToClassList(UssStyles.AssetPreview);
            m_DraggableImage.AddToClassList(UssStyles.Thumbnail);
            m_AssetTypeIcon.AddToClassList(UssStyles.AssetTypeIcon);

            m_ImportedStatusIcon = new VisualElement();
            m_ImportedStatusIcon.AddToClassList(UssStyles.ImportedStatus);

            Add(m_DraggableImage);
            Add(m_AssetTypeIcon);
            Add(m_ImportedStatusIcon);
        }

        public interface IStatus
        {
            string Description { get; }
            VisualElement CreateVisualTree();
        }

        public void SetStatus(IStatus status)
        {
            UIElementsUtils.SetDisplay(m_ImportedStatusIcon, status != null);

            m_ImportedStatusIcon.Clear();

            if (status == null)
                return;

            m_ImportedStatusIcon.Add(status.CreateVisualTree());
            m_ImportedStatusIcon.tooltip = status.Description;
        }

        public void SetAssetType(string extension, bool useAssetTypeAsTooltip)
        {
            var icon = AssetDataTypeHelper.GetIconForExtension(extension);

            UIElementsUtils.Show(m_AssetTypeIcon);
            m_AssetTypeIcon.EnableInClassList(UssStyles.DefaultAssetIcon, icon == null);
            m_AssetTypeIcon.style.backgroundImage = icon == null ? StyleKeyword.Null : icon;
            m_AssetTypeIcon.tooltip = useAssetTypeAsTooltip ? extension : null;
        }

        public void ClearPreview()
        {
            SetThumbnail(null);

            m_AssetTypeIcon.EnableInClassList(UssStyles.DefaultAssetIcon, false);
            m_AssetTypeIcon.style.backgroundImage = StyleKeyword.Null;
            m_AssetTypeIcon.tooltip = string.Empty;
        }

        public void SetThumbnail(Texture2D texture2D)
        {
            EnableInClassList("no-thumbnail", texture2D == null);
            m_DraggableImage.SetBackgroundImage(texture2D);
        }
    }
}
