using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    interface IAuthoringData
    {
        [DataMember(Name = "createdBy")]
        string CreatedBy { get; }

        [DataMember(Name = "created")]
        DateTime? Created { get; }

        [DataMember(Name = "updatedBy")]
        string UpdatedBy { get; }

        [DataMember(Name = "updated")]
        DateTime? Updated { get; }
    }
}
