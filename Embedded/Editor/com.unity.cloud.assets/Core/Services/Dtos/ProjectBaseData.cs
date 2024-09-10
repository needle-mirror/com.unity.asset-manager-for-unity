using System.Runtime.Serialization;

namespace Unity.Cloud.AssetsEmbedded
{
    [DataContract]
    class ProjectBaseData : IProjectBaseData
    {
        /// <inheritdoc/>
        public string Name { get; set; }

        /// <inheritdoc/>
        public IDeserializable Metadata { get; set; }
    }
}
