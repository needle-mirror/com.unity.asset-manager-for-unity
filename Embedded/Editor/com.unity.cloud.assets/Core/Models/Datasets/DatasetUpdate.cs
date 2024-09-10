using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Cloud.AssetsEmbedded
{
    class DatasetUpdate : DatasetInfo, IDatasetUpdate
    {
        /// <inheritdoc />
        public IReadOnlyList<string> FileOrder { get; set; }

        public DatasetUpdate() { }

        public DatasetUpdate(string name)
            : base(name) { }

        public DatasetUpdate(IDataset dataset)
            : base(dataset)
        {
            FileOrder = dataset.FileOrder?.ToList();
            IsVisible = dataset.IsVisible;
        }
    }
}
