using System;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// A structure for defining the criteria of an <see cref="IDataset"/> search request.
    /// </summary>
    class DatasetSearchCriteria : CompoundSearchCriteria
    {
        /// <inheritdoc cref="IDataset.Name"/>
        public StringSearchCriteria Name { get; } = new(nameof(IDataset.Name), "name");

        /// <inheritdoc cref="IDataset.Description"/>
        public StringSearchCriteria Description { get; } = new(nameof(IDataset.Description), "description");

        /// <inheritdoc cref="IDataset.Status"/>
        public SearchCriteria<string> Status { get; } = new(nameof(IDataset.Status), "status");

        /// <inheritdoc cref="IDataset.Tags"/>
        public ListSearchCriteria<string> Tags { get; } = new(nameof(IDataset.Tags), "tags");

        /// <inheritdoc cref="IDataset.SystemTags"/>
        public ListSearchCriteria<string> SystemTags { get; } = new(nameof(IDataset.SystemTags), "systemTags");

        /// <inheritdoc cref="IDataset.AuthoringInfo"/>
        public AuthoringInfoSearchFilter AuthoringInfo { get; } = new(nameof(IDataset.AuthoringInfo), string.Empty);

        /// <inheritdoc cref="DatasetEntity.Metadata"/>
        public MetadataSearchCriteria Metadata { get; } = new(nameof(DatasetEntity.Metadata), "metadata");

        /// <inheritdoc cref="IDataset.IsVisible"/>
        public NullableSearchCriteria<bool> IsVisible { get; } = new(nameof(IDataset.IsVisible), "isVisible");

        internal DatasetSearchCriteria(string propertyName, string searchKey)
            : base(propertyName, searchKey) { }
    }
}
