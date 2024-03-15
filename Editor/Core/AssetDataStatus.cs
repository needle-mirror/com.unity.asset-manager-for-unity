using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    static class AssetDataStatus
    {
        // Import
        public static readonly AssetPreview.IStatus Imported = new PreviewStatus("Asset is imported", UssStyles.StatusIcon, UssStyles.StatusImported);
        public static readonly AssetPreview.IStatus UpToDate = new PreviewStatus("Asset is up to date", UssStyles.StatusUpToDate);
        public static readonly AssetPreview.IStatus OutOfDate = new PreviewStatus("Asset is outdated", UssStyles.StatusOutOfDate);
        public static readonly AssetPreview.IStatus Error = new PreviewStatus("Asset was deleted or is not accessible", UssStyles.StatusError);

        // Upload
        public static readonly AssetPreview.IStatus Linked = new PreviewStatus("This asset is a dependency of another asset", "grid-view--item-linked");
        public static readonly AssetPreview.IStatus UploadSkip = new PreviewStatus("This asset already exists on the cloud and will not be uploaded", "grid-view--item-upload-skip");
        public static readonly AssetPreview.IStatus UploadOverride = new PreviewStatus("This asset will override its cloud version", "grid-view--item-upload-override");
        public static readonly AssetPreview.IStatus UploadDuplicate = new PreviewStatus("This asset already exists on the cloud but a new cloud asset will be uploaded", "grid-view--item-upload-duplicate");

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
