using System;
using System.Runtime.Serialization;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    [DataContract]
#pragma warning disable CS0649 // Fields populated by [DataMember] reflection-based deserialization
    struct AssetTrashDetailsData
    {
        /// <summary>
        /// The project ID where the asset is currently trashed.
        /// </summary>
        [DataMember(Name = "projectId")]
        public ProjectId ProjectId;

        /// <summary>
        /// The user who moved the asset to trash.
        /// </summary>
        [DataMember(Name = "movedToTrashBy")]
        public string MovedToTrashBy;

        /// <summary>
        /// The date and time when the asset was moved to trash.
        /// </summary>
        [DataMember(Name = "movedToTrashAt")]
        public DateTime? MovedToTrashAt;
    }
#pragma warning restore CS0649
}
