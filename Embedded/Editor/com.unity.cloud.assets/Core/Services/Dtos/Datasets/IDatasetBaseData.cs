using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Unity.Cloud.AssetsEmbedded
{
    interface IDatasetBaseData : IMetadataInfo
    {
        [DataMember(Name = "name")]
        string Name { get; }

        [DataMember(Name = "description")]
        string Description { get; }

        [DataMember(Name = "tags")]
        IEnumerable<string> Tags { get; }
    }
}
