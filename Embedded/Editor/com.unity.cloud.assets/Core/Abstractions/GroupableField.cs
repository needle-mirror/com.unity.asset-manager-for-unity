using System.Runtime.Serialization;

namespace Unity.Cloud.AssetsEmbedded
{
    [DataContract]
enum GroupableField
    {
        [EnumMember(Value = "name")]
        Name,
        [EnumMember(Value = "assetVersion")]
        Version,
        [EnumMember(Value = "primaryType")]
        Type,
        [EnumMember(Value = "status")]
        Status,
        [EnumMember(Value = "tags")]
        Tags,
        [EnumMember(Value = "systemTags")]
        SystemTags,
        [EnumMember(Value = "previewFile")]
        PreviewFile,
        [EnumMember(Value = "createdBy")]
        CreatedBy,
        [EnumMember(Value = "updatedBy")]
        UpdateBy
    }
}
