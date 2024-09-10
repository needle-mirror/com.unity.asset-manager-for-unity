using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Unity.Cloud.AssetsEmbedded
{
    [DataContract]
    class DatasetBaseData : IDatasetBaseData
    {
        /// <inheritdoc />
        public string Name { get; set; }

        /// <inheritdoc />
        public string Description { get; set; }

        /// <inheritdoc />
        public List<string> Tags { get; set; } = new List<string>();// For now initialize list

        /// <inheritdoc />
        public Dictionary<string, object> Metadata { get; set; }

        /// <inheritdoc />
        public Dictionary<string, object> SystemMetadata { get; set; }
    }
}
