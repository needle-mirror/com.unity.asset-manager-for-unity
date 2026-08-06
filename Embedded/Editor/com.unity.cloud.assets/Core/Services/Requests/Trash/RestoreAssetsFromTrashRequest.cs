using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// RestoreAssetsFromTrashRequest
    /// Represents a POST request to restore assets from the trash.
    /// </summary>
    [DataContract]
    class RestoreAssetsFromTrashRequest : ProjectOrLibraryRequest
    {
        [DataMember(Name = "assetIds")]
        AssetId[] m_AssetIds;

        public RestoreAssetsFromTrashRequest(ProjectId projectId, IEnumerable<AssetId> assetIds) : base(projectId)
        {
            m_RequestUrl += "/assets/restore";

            m_AssetIds = assetIds.ToArray();
        }

        /// <inheritdoc />
        public override HttpContent ConstructBody()
        {
            if (m_AssetIds == null)
            {
                return base.ConstructBody();
            }

            var body = IsolatedSerialization.SerializeWithDefaultConverters(this);

            return new StringContent(body, Encoding.UTF8, "application/json");
        }
    }
}
