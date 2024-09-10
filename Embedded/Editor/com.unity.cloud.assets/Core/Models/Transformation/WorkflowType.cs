using System.Runtime.Serialization;

namespace Unity.Cloud.AssetsEmbedded
{
    [DataContract]
enum WorkflowType
    {
        [EnumMember(Value = "thumbnail-generator")]
        Thumbnail_Generation,
        [EnumMember(Value = "glb-preview")]
        GLB_Preview,
        [EnumMember(Value = "3d-data-streaming")]
        Data_Streaming,
        [EnumMember(Value = "transcode-video")]
        Transcode_Video,
        Metadata_Extraction,
        Generic_Polygon_Target,
        Custom
    }
}
