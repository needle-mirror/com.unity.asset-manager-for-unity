using System;

namespace Unity.Cloud.AssetsEmbedded
{
    class PendingFileData : FileCreateData, IPendingFileData
    {
        public Uri UploadUrl { get; set; }
    }
}
