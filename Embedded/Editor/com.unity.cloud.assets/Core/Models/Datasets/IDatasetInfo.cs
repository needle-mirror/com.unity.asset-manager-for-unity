using System.Collections.Generic;

namespace Unity.Cloud.AssetsEmbedded
{
    interface IDatasetInfo
    {
        /// <inheritdoc cref="IDataset.Name"/>
        string Name { get; }

        /// <inheritdoc cref="IDataset.Description"/>
        string Description { get; }

        /// <inheritdoc cref="IDataset.Tags"/>
        List<string> Tags { get; }

        /// <inheritdoc cref="IDataset.IsVisible"/>
        bool? IsVisible { get; }
    }
}
