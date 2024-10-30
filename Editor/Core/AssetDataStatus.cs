using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    static class AssetDataStatus
    {
        // Import
        public static readonly AssetPreview.IStatus Imported = new PreviewStatus(Constants.ImportedText, Constants.ReimportActionText, UssStyles.StatusIcon, UssStyles.StatusImported);
        public static readonly AssetPreview.IStatus UpToDate = new PreviewStatus(Constants.UpToDateText, Constants.ReimportActionText, UssStyles.StatusUpToDate);
        public static readonly AssetPreview.IStatus OutOfDate = new PreviewStatus(Constants.OutOfDateText, Constants.UpdateToLatestActionText, UssStyles.StatusOutOfDate);
        public static readonly AssetPreview.IStatus Error = new PreviewStatus(Constants.StatusErrorText, string.Empty, UssStyles.StatusError);

        // Upload
        public static readonly AssetPreview.IStatus Linked = new PreviewStatus(Constants.LinkedText, string.Empty, "grid-view--item-linked");
        public static readonly AssetPreview.IStatus UploadAdd = new PreviewStatus(Constants.UploadAddText, string.Empty, "grid-view--item-upload-add");
        public static readonly AssetPreview.IStatus UploadSkip = new PreviewStatus(Constants.UploadSkipText, string.Empty, "grid-view--item-upload-skip");
        public static readonly AssetPreview.IStatus UploadOverride = new PreviewStatus(Constants.UploadNewVersionText, string.Empty, "grid-view--item-upload-override");
        public static readonly AssetPreview.IStatus UploadDuplicate = new PreviewStatus(Constants.UploadDuplicateText, string.Empty, "grid-view--item-upload-duplicate");
        public static readonly AssetPreview.IStatus UploadOutside = new PreviewStatus(Constants.UploadOutsideText, string.Empty, "grid-view--item-upload-outside");

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
        readonly string[] m_Styles;

        public string Description { get; }
        public string ActionText { get; }

        public PreviewStatus(string description, string actionText, params string[] ussStyles)
        {
            Description = description;
            ActionText = actionText;
            m_Styles = ussStyles;
        }

        public VisualElement CreateVisualTree()
        {
            var visualElement = new VisualElement();
            foreach (var style in m_Styles)
            {
                visualElement.AddToClassList(style);
            }

            return visualElement;
        }
    }
}
