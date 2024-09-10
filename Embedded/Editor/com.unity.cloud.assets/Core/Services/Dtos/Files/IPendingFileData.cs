using System;

namespace Unity.Cloud.AssetsEmbedded
{
    interface IPendingFileData : IFileCreateData
    {
        Uri UploadUrl { get; }
    }
}
