using System;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// A structure for defining the criteria of an <see cref="IAsset"/> search request.
    /// </summary>
    class AssetSearchCriteria : CompoundSearchCriteria
    {
        /// <inheritdoc cref="AssetId"/>
        public SearchCriteria<string> Id { get; } = new(nameof(AssetDescriptor.AssetId), "assetId");

        /// <inheritdoc cref="AssetVersion"/>
        public SearchCriteria<string> Version { get; } = new(nameof(AssetDescriptor.AssetVersion), "assetVersion");

        /// <inheritdoc cref="IAsset.State"/>
        public AssetStateSearchCriteria State { get; } = new(nameof(IAsset.State));

        /// <inheritdoc cref="IAsset.IsFrozen"/>
        [Obsolete("Use State instead.")]
        public NullableSearchCriteria<bool> IsFrozen { get; } = new(nameof(IAsset.IsFrozen), "isFrozen");

        /// <inheritdoc cref="IAsset.FrozenSequenceNumber"/>
        public NullableSearchCriteria<int> FrozenSequenceNumber { get; } = new(nameof(IAsset.FrozenSequenceNumber), "versionNumber");

        /// <inheritdoc cref="IAsset.ParentVersion"/>
        public SearchCriteria<string> ParentVersion { get; } = new(nameof(IAsset.ParentVersion), "parentAssetVersion");

        /// <inheritdoc cref="IAsset.ParentFrozenSequenceNumber"/>
        public NullableSearchCriteria<int> ParentFrozenSequenceNumber { get; } = new(nameof(IAsset.ParentFrozenSequenceNumber), "parentVersionNumber");

        /// <inheritdoc cref="IAsset.Name"/>
        public StringSearchCriteria Name { get; } = new(nameof(IAsset.Name), "name");

        /// <inheritdoc cref="IAsset.Description"/>
        public StringSearchCriteria Description { get; } = new(nameof(IAsset.Description), "description");

        /// <inheritdoc cref="IAsset.Type"/>
        public AssetTypeSearchCriteria Type { get; } = new(nameof(IAsset.Type));

        /// <inheritdoc cref="IAsset.StatusName"/>
        public SearchCriteria<string> Status { get; } = new("Status", "status");

        /// <inheritdoc cref="IAsset.Tags"/>
        public ListSearchCriteria<string> Tags { get; } = new(nameof(IAsset.Tags), "tags");

        /// <inheritdoc cref="IAsset.SystemTags"/>
        public ListSearchCriteria<string> SystemTags { get; } = new(nameof(IAsset.SystemTags), "systemTags");

        /// <inheritdoc cref="IAsset.Labels"/>
        public ListSearchCriteria<string> Labels { get; } = new(nameof(IAsset.Labels), "labels");

        /// <inheritdoc cref="IAsset.ArchivedLabels"/>
        public ListSearchCriteria<string> ArchivedLabels { get; } = new(nameof(IAsset.ArchivedLabels), "archivedLabels");

        /// <inheritdoc cref="IAsset.Metadata"/>
        public MetadataSearchCriteria Metadata { get; } = new(nameof(IAsset.Metadata), "metadata");

        /// <inheritdoc cref="IAsset.PreviewFile"/>
        public StringSearchCriteria PreviewFile { get; } = new("PreviewFile", "previewFile");

        /// <inheritdoc cref="IAsset.SourceProject"/>
        public SearchCriteria<string> SourceProjectId { get; } = new(nameof(IAsset.SourceProject), "sourceProjectId");

        /// <inheritdoc cref="IAsset.AuthoringInfo"/>
        public AuthoringInfoSearchFilter AuthoringInfo { get; } = new(nameof(IAsset.AuthoringInfo), string.Empty);

        /// <inheritdoc cref="IFile"/>
        public FileSearchCriteria Files { get; } = new("Files", "files");

        /// <inheritdoc cref="IDataset"/>
        public DatasetSearchCriteria Datasets { get; } = new("Datasets", "datasets");

        internal AssetSearchCriteria()
            : base(string.Empty, string.Empty) { }
    }
}
