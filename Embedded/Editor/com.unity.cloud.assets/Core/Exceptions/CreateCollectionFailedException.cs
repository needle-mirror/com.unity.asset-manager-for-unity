using System;
using System.Runtime.Serialization;

namespace Unity.Cloud.AssetsEmbedded
{
    [Serializable]
sealed class CreateCollectionFailedException : Exception
    {
        public CreateCollectionFailedException(string message)
            : base(message) { }

        public CreateCollectionFailedException(string message, Exception innerException)
            : base(message, innerException) { }

        CreateCollectionFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
}
