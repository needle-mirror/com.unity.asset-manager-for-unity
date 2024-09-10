using System;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// A structure for defining the criteria of an <see cref="IFile"/> search request.
    /// </summary>
    class FileSearchCriteria : CompoundSearchCriteria
    {
        /// <inheritdoc cref="FileDescriptor.Path"/>
        public StringSearchCriteria Path { get; } = new(nameof(FileDescriptor.Path), "filePath");

        /// <inheritdoc cref="IFile.Description"/>
        public StringSearchCriteria Description { get; } = new(nameof(IFile.Description), "description");

        /// <inheritdoc cref="IFile.Status"/>
        public SearchCriteria<string> Status { get; } = new(nameof(IFile.Status), "status");

        /// <inheritdoc cref="IFile.Tags"/>
        public ListSearchCriteria<string> Tags { get; } = new(nameof(IFile.Tags), "tags");

        /// <inheritdoc cref="IFile.SystemTags"/>
        public ListSearchCriteria<string> SystemTags { get; } = new(nameof(IFile.SystemTags), "systemTags");

        /// <inheritdoc cref="IFile.AuthoringInfo"/>
        public AuthoringInfoSearchFilter AuthoringInfo { get; } = new(nameof(IFile.AuthoringInfo), string.Empty);

        /// <inheritdoc cref="IFile.SizeBytes"/>
        public SearchCriteria<long> SizeBytes { get; } = new(nameof(IFile.SizeBytes), "sizeBytes");

        /// <inheritdoc cref="FileEntity.Metadata"/>
        public MetadataSearchCriteria Metadata { get; } = new(nameof(FileEntity.Metadata), "metadata");

        internal FileSearchCriteria(string propertyName, string searchKey)
            : base(propertyName, searchKey) { }
    }
}
