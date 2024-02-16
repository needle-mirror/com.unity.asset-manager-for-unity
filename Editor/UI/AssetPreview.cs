using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class AssetPreview : VisualElement
    {
        static class UssStyles
        {
            public static readonly string Thumbnail = "asset-preview-thumbnail";
            public static readonly string AssetPreview = "asset-preview";
            public static readonly string AssetTypeIcon = "asset-preview-asset-type-icon";
            public static readonly string DefaultAssetIcon = "default-asset-icon";
            public static readonly string ImportedStatus = Constants.GridItemStyleClassName + "-imported_status";
            public static readonly string ImportedStatusImported = ImportedStatus + "-imported";
            public static readonly string ImportedStatusUpToDate = ImportedStatus + "-up_to_date";
            public static readonly string ImportedStatusOutOfDate = ImportedStatus + "-out_of_date";
            public static readonly string ImportedStatusError = ImportedStatus + "-error";
        }

        private readonly DraggableImage m_DraggableImage;
        private readonly VisualElement m_AssetTypeIcon;
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
        
        public void SetImportStatusIcon(ImportedStatus importedStatus)
        {
            UIElementsUtils.SetDisplay(m_ImportedStatusIcon, importedStatus != ImportedStatus.None);

            m_ImportedStatusIcon.RemoveFromClassList(UssStyles.ImportedStatusImported);
            m_ImportedStatusIcon.RemoveFromClassList(UssStyles.ImportedStatusUpToDate);
            m_ImportedStatusIcon.RemoveFromClassList(UssStyles.ImportedStatusOutOfDate);
            m_ImportedStatusIcon.RemoveFromClassList(UssStyles.ImportedStatusError);
            
            var tooltipStr = "Asset is imported";
            
            switch (importedStatus)
            {
                case ImportedStatus.UpToDate:
                    m_ImportedStatusIcon.AddToClassList(UssStyles.ImportedStatusUpToDate);
                    tooltipStr = "Asset is up to date";
                    break;
                
                case ImportedStatus.OutDated:
                    m_ImportedStatusIcon.AddToClassList(UssStyles.ImportedStatusOutOfDate);
                    tooltipStr = "Asset is outdated";
                    break;
                
                case ImportedStatus.Error:
                    m_ImportedStatusIcon.AddToClassList(UssStyles.ImportedStatusError);
                    tooltipStr = "Asset was deleted or is not accessible";
                    break;
                default:
                    m_ImportedStatusIcon.AddToClassList(UssStyles.ImportedStatusImported);
                    break;
            }

            m_ImportedStatusIcon.tooltip = tooltipStr;
        }

        public void SetAssetType(string extension, bool useAssetTypeAsTooltip)
        {
            var icon = AssetDataTypeHelper.GetIconForExtension(extension);

            m_AssetTypeIcon.EnableInClassList(UssStyles.DefaultAssetIcon, icon == null);
            m_AssetTypeIcon.style.backgroundImage = icon == null ? StyleKeyword.Null : icon;
            m_AssetTypeIcon.tooltip = useAssetTypeAsTooltip ? extension : string.Empty;
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
