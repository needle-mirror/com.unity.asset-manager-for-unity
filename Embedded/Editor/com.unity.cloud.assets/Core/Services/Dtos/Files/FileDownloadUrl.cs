using System;

namespace Unity.Cloud.AssetsEmbedded
{
    struct FileDownloadUrl
    {
        public string FilePath { get; set; }
        public Uri DownloadUrl { get; set; }
    }
}
