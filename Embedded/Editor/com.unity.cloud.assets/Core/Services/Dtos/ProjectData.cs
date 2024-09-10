using System.Runtime.Serialization;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    [DataContract]
    class ProjectData : ProjectBaseData, IProjectData
    {
        /// <inheritdoc/>
        public ProjectId Id { get; }

        public ProjectData(string id)
            : this(new ProjectId(id)) { }

        internal ProjectData(ProjectId id)
        {
            Id = id;
        }
    }
}
