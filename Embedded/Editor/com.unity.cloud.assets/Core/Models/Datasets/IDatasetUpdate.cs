using System.Collections.Generic;

namespace Unity.Cloud.AssetsEmbedded
{
    interface IDatasetUpdate : IDatasetInfo
    {
        /// <inheritdoc cref="IDataset.FileOrder"/>
        IReadOnlyList<string> FileOrder { get; }
    }
}
