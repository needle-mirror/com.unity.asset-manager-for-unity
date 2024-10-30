using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    [DataContract]
    class SubmitVersionRequest : AssetRequest
    {
        [DataMember(Name = "changeLog")]
        readonly string m_ChangeLog;

        public SubmitVersionRequest(ProjectId projectId, AssetId assetId, AssetVersion assetVersion, string changeLog)
            : base(projectId, assetId, assetVersion)
        {
            m_RequestUrl += $"/submit";

            m_ChangeLog = changeLog;
        }

        public override HttpContent ConstructBody()
        {
            var body = IsolatedSerialization.Serialize(this, IsolatedSerialization.defaultSettings);
            return new StringContent(body, Encoding.UTF8, "application/json");
        }
    }
}
