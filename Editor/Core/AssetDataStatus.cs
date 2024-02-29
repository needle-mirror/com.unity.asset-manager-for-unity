using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    static class AssetDataStatus
    {
        public static readonly AssetPreview.IStatus Imported = new PreviewStatus("Asset is imported", UssStyles.StatusIcon, UssStyles.StatusImported);
        public static readonly AssetPreview.IStatus UpToDate = new PreviewStatus("Asset is up to date", UssStyles.StatusIcon, UssStyles.StatusUpToDate);
        public static readonly AssetPreview.IStatus OutOfDate = new PreviewStatus("Asset is outdated", UssStyles.StatusIcon, UssStyles.StatusOutOfDate);
        public static readonly AssetPreview.IStatus Error = new PreviewStatus("Asset was deleted or is not accessible", UssStyles.StatusIcon, UssStyles.StatusError);
        public static readonly AssetPreview.IStatus Linked = new PreviewStatus("This asset is dependency of another asset", "grid-view--item-linked");

        static class UssStyles
        {
            public static readonly string StatusIcon = "grid-view--status-icon";
            static readonly string k_Status = Constants.GridItemStyleClassName + "-imported_status";
            public static readonly string StatusImported = k_Status + "-imported";
            public static readonly string StatusUpToDate = k_Status + "-up_to_date";
            public static readonly string StatusOutOfDate = k_Status + "-out_of_date";
            public static readonly string StatusError = k_Status + "-error";
        }
    }

    class PreviewStatus : AssetPreview.IStatus
    {
        public string Description { get; }

        readonly string[] m_Styles;

        public VisualElement CreateVisualTree()
        {
            var visualElement = new VisualElement();
            foreach (var style in m_Styles)
            {
                visualElement.AddToClassList(style);
            }

            return visualElement;
        }

        public PreviewStatus(string description, params string[] ussStyles)
        {
            Description = description;
            m_Styles = ussStyles;
        }
    }
}
