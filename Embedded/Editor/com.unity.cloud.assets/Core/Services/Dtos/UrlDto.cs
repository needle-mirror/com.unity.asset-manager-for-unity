using System.Runtime.Serialization;

namespace Unity.Cloud.AssetsEmbedded
{
    [DataContract]
    struct UrlDto
    {
        [DataMember(Name = "url")]
        public string Url { get; set; }
    }
}
