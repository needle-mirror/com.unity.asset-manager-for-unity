using System;
using System.Net.Http;
using System.Text;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    class DatasetRequest : AssetRequest
    {
        readonly IDatasetBaseData m_Data;

        /// <summary>
        /// Get a single dataset by id.
        /// </summary>
        /// <param name="projectId">ID of the project</param>
        /// <param name="assetId">ID of the asset</param>
        /// <param name="assetVersion">ID of the asset version</param>
        /// <param name="datasetId">ID of the dataset</param>
        /// <param name="data">The object containing the data of the dataset</param>
        public DatasetRequest(ProjectId projectId, AssetId assetId, AssetVersion assetVersion, DatasetId datasetId, IDatasetBaseData data = null)
            : base(projectId, assetId, assetVersion)
        {
            m_RequestUrl += $"/datasets/{datasetId}";

            m_Data = data;
        }

        /// <summary>
        /// Get a single dataset by id.
        /// </summary>
        /// <param name="projectId">ID of the project</param>
        /// <param name="assetId">ID of the asset</param>
        /// <param name="assetVersion">Version of the asset</param>
        /// <param name="datasetId">ID of the dataset</param>
        /// <param name="includedDatasetFields">Sets the fields to be included in the response.</param>
        public DatasetRequest(ProjectId projectId, AssetId assetId, AssetVersion assetVersion, DatasetId datasetId, DatasetFields includedDatasetFields)
            : this(projectId, assetId, assetVersion, datasetId)
        {
            includedDatasetFields.Parse(AddFieldFilterToQueryParams);
        }

        public DatasetRequest(ProjectId projectId, AssetId assetId, AssetVersion assetVersion, DatasetFields includedFieldsFilter, string token = null, int? limit = null)
            : base(projectId, assetId, assetVersion)
        {
            m_RequestUrl += "/datasets";

            includedFieldsFilter.Parse(AddFieldFilterToQueryParams);

            AddParamToQuery("Limit", limit?.ToString());
            AddParamToQuery("Token", token);
        }

        public override HttpContent ConstructBody()
        {
            if (m_Data == null)
            {
                return base.ConstructBody();
            }

            var body = IsolatedSerialization.Serialize(m_Data, IsolatedSerialization.defaultSettings);
            return new StringContent(body, Encoding.UTF8, "application/json");
        }
    }
}
