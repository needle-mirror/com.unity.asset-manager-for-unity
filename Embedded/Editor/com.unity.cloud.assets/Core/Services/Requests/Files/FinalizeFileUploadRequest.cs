using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// Represents a finalized upload asset file request.
    /// </summary>
    class FinalizeFileUploadRequestV1 : FileRequest
    {
        /// <summary>
        /// Creates an Asset File Request Object.
        /// Creates a single asset file.
        /// </summary>
        /// <param name="projectId">ID of the project.</param>
        /// <param name="assetId">The id of the asset the file will linked to.</param>
        /// <param name="assetVersion">The version of the asset the file will linked to.</param>
        /// <param name="filePath">The path to the file for which the upload will be finalized.</param>
        /// <param name="disableAutomaticTransformations">If true, automatic transformations, such as preview generation and metadata extraction, will be disabled.</param>
        public FinalizeFileUploadRequestV1(ProjectId projectId, AssetId assetId, AssetVersion assetVersion, string filePath, bool disableAutomaticTransformations)

            : base(projectId, assetId, assetVersion, filePath)
        {
            m_RequestUrl += $"/finalize";
            if (disableAutomaticTransformations)
            {
                m_RequestUrl += "?enableCheckForPreviewAndMetadataGeneration=false";
            }
        }
    }

    /// <summary>
    /// Represents a finalized upload asset file request.
    /// </summary>
    class FinalizeFileUploadRequestV2 : FileRequest
    {
        /// <summary>
        /// Creates an Asset File Request Object.
        /// Creates a single asset file.
        /// </summary>
        /// <param name="projectId">ID of the project.</param>
        /// <param name="assetId">The id of the asset the file will linked to.</param>
        /// <param name="assetVersion">The version of the asset the file will linked to.</param>
        /// <param name="datasetId">The id of the dataset the file will linked to.</param>
        /// <param name="filePath">The path to the file for which the upload will be finalized.</param>
        /// <param name="disableAutomaticTransformations">If true, automatic transformations, such as preview generation and metadata extraction, will be disabled.</param>
        public FinalizeFileUploadRequestV2(ProjectId projectId, AssetId assetId, AssetVersion assetVersion, DatasetId datasetId, string filePath, bool disableAutomaticTransformations)

            : base(projectId, assetId, assetVersion, datasetId, filePath)

        {
            m_RequestUrl += $"/finalize";
            if (disableAutomaticTransformations)
            {
                m_RequestUrl += "?enableCheckForPreviewAndMetadataGeneration=false";
            }
        }
    }
}
