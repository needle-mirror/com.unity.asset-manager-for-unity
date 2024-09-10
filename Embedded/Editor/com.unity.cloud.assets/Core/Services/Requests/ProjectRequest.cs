using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// Base class for api requests on assets.
    /// </summary>
    class ProjectRequest : ApiRequest
    {
        /// <summary>
        /// AssetRequest Request Object.
        /// </summary>
        /// <param name="projectId">ID of the project</param>
        public ProjectRequest(ProjectId projectId)
        {
            m_RequestUrl = $"/projects/{projectId}";
        }
    }
}
