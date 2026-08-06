using System.Runtime.Serialization;

namespace Unity.Cloud.AssetsEmbedded
{
    [DataContract]
    struct CreatedProjectDto
    {
        [DataMember(Name = "projectId")]
        public string Id { get; set; }
    }
}
