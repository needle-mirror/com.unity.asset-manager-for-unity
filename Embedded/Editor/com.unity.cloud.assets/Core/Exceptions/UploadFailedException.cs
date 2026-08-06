using System;
using System.Runtime.Serialization;

namespace Unity.Cloud.AssetsEmbedded
{
    [Serializable]
sealed class UploadFailedException : Exception
    {
        public UploadFailedException(string message)
            : base(message) { }

        UploadFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
