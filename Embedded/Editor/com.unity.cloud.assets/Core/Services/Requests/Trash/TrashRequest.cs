using System.Collections.Generic;
using System.Linq;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// Base request class for accessing the trash
    /// </summary>
    class TrashRequest : ApiRequest
    {
        protected TrashRequest(OrganizationId organizationId)
        {
            m_RequestUrl = $"/organizations/{organizationId}/trash/assets";
        }

        protected TrashRequest(ProjectId projectId)
        {
            m_RequestUrl = $"/projects/{projectId}/trash/assets";
        }

        /// <summary>
        /// Creates a request to permanently delete assets from the trash.
        /// </summary>
        /// <returns>A <see cref="TrashRequest"/>. </returns>
        public static TrashRequest DeleteAssets(ProjectId projectId, IEnumerable<AssetId> assetIds)
        {
            var request = new TrashRequest(projectId);
            request.AddParamToQuery("assetIds", assetIds.Select(id => id.ToString()));

            return request;
        }

        /// <summary>
        /// Creates a request to permanently delete all assets from the trash.
        /// </summary>
        /// <returns>A <see cref="TrashRequest"/>. </returns>
        public static TrashRequest Empty(ProjectId projectId)
        {
            var request = new TrashRequest(projectId);
            request.m_RequestUrl += "/all";

            return request;
        }
    }
}
