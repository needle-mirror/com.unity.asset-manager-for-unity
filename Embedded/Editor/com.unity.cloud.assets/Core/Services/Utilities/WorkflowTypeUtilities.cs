using System;

namespace Unity.Cloud.AssetsEmbedded
{
    static class WorkflowTypeUtilities
    {
        public static string ToJsonValue(this WorkflowType workflowType)
        {
            return workflowType switch
            {
                WorkflowType.Thumbnail_Generation => "thumbnail-generator",
                WorkflowType.GLB_Preview => "glb-preview",
                WorkflowType.Data_Streaming => "3d-data-streaming",
                WorkflowType.Transcode_Video => "video-transcoding",
                WorkflowType.Metadata_Extraction => "metadata-extraction",
                WorkflowType.Generic_Polygon_Target => "generic-polygon-target",
                _ => string.Empty
            };
        }

        public static WorkflowType FromJsonValue(string value)
        {
            return value switch
            {
                "thumbnail-generator" => WorkflowType.Thumbnail_Generation,
                "glb-preview" => WorkflowType.GLB_Preview,
                "3d-data-streaming" => WorkflowType.Data_Streaming,
                "video-transcoding" => WorkflowType.Transcode_Video,
                "metadata-extraction" => WorkflowType.Metadata_Extraction,
                "generic-polygon-target" => WorkflowType.Generic_Polygon_Target,
                _ => default
            };
        }
    }
}
