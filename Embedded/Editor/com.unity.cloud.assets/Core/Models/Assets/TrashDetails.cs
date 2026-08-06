using System;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// The details telling in which project the asset is trashed.
    /// </summary>
    struct TrashDetails
    {
        /// <summary>
        /// The project from which the asset was moved to trash.
        /// </summary>
        public ProjectId ProjectId { get; internal set;  }

        /// <summary>
        /// The ID of the user who moved the asset to trash.
        /// </summary>
        public UserId MovedToTrashBy { get; internal set; }

        /// <summary>
        /// The date and time when the asset was moved to trash.
        /// </summary>
        public DateTime? MovedToTrashAt { get; internal set; }
    }
}
