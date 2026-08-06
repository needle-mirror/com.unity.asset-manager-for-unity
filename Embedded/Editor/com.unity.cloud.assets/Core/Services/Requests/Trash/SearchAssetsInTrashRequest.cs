using System.Net.Http;
using System.Text;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// SearchAssetsInProjectTrashRequest
    /// Search assets in a project's trash based on criteria.
    /// </summary>
    class SearchAssetsInTrashRequest : TrashRequest
    {
        readonly SearchRequestParameters m_Parameters;

        /// <summary>
        /// Across projects in trash search Request Object.
        /// Across projects in trash search assets based on criteria.
        /// </summary>
        /// <param name="organizationId">Genesis ID of the organization</param>
        /// <param name="parameters">The search asset request criteria.</param>
        public SearchAssetsInTrashRequest(OrganizationId organizationId, SearchRequestParameters parameters)
            : base(organizationId)
        {
            m_RequestUrl += "/search";

            m_Parameters = parameters;
        }

        /// <summary>
        /// Creates an instance of a <see cref="SearchAssetsInTrashRequest"/> for a project.
        /// Search assets in trash based on criteria.
        /// </summary>
        /// <param name="projectId">ID of the project.</param>
        /// <param name="parameters">The search asset request criteria.</param>
        public SearchAssetsInTrashRequest(ProjectId projectId, SearchRequestParameters parameters) : base(projectId)
        {
            m_RequestUrl += "/search";

            m_Parameters = parameters;
        }

        /// <inheritdoc />
        public override HttpContent ConstructBody()
        {
            var body = IsolatedSerialization.SerializeWithDefaultConverters(m_Parameters);
            return new StringContent(body, Encoding.UTF8, "application/json");
        }
    }
}
