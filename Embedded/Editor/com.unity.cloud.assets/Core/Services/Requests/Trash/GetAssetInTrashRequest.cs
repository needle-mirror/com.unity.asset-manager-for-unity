using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// GetAssetInTrashRequest
    /// Get assets in a project's trash based on criteria.
    /// </summary>
    class GetAssetInTrashRequest : TrashRequest
    {
        /// <summary>
        /// Creates an instance of a <see cref="SearchAssetsInTrashRequest"/> for a project.
        /// Search assets in trash based on criteria.
        /// </summary>
        public GetAssetInTrashRequest(ProjectId projectId, AssetId assetId, AssetVersion assetVersion, FieldsFilter includedFieldsFilter) : base(projectId)
        {
            m_RequestUrl += $"/{assetId}/versions/{assetVersion}";

            includedFieldsFilter?.Parse(AddFieldFilterToQueryParams);
        }

        void AddFieldFilterToQueryParams(string value)
        {
            AddParamToQuery("IncludeFields", value);
        }
    }
}

