using System;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// A structure for defining the criteria of a <see cref="TrashDetails"/> search request.
    /// </summary>
    class TrashDetailsSearchCriteria : CompoundSearchCriteria
    {
        internal TrashDetailsSearchCriteria(string propertyName, string searchKey) : base(propertyName, searchKey) { }

        /// <inheritdoc cref="TrashDetails.ProjectId"/>
        public SearchCriteria<string> ProjectId { get; } = new(nameof(TrashDetails.ProjectId), "trashDetails.projectId");

        /// <inheritdoc cref="TrashDetails.MovedToTrashAt"/>
        public ConditionalSearchCriteria<DateTime> MovedToTrashAt { get; } = new(nameof(TrashDetails.MovedToTrashAt), "trashDetails.movedToTrashAt", SearchConditionData.DateRangeType);

        /// <inheritdoc cref="TrashDetails.MovedToTrashBy"/>
        public SearchCriteria<string> MovedToTrashBy { get; } = new(nameof(TrashDetails.MovedToTrashBy), "trashDetails.movedToTrashBy");

    }
}
