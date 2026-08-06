using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Unity.Cloud.AssetsEmbedded
{
    [DataContract]
    struct DownloadUrl
    {
        [DataMember(Name = "datasetId")]
        public string DatasetId { get; set; }

        [DataMember(Name = "filePath")]
        public string Path { get; set; }

        [DataMember(Name = "url")]
        public string Url { get; set; }
    }

    [DataContract]
    struct DownloadUrls
    {
        [DataMember(Name = "files")]
        public List<DownloadUrl> FileUrls { get; set; }
    }
}
