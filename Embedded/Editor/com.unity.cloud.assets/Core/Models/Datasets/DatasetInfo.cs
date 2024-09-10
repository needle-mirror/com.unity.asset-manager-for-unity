using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Cloud.AssetsEmbedded
{
    abstract class DatasetInfo : IDatasetInfo
    {
        /// <inheritdoc />
        public string Name { get; set; }

        /// <inheritdoc />
        public string Description { get; set; }

        /// <inheritdoc />
        public List<string> Tags { get; set; }

        /// <inheritdoc />
        public bool? IsVisible { get; set; }

        protected DatasetInfo() { }

        protected DatasetInfo(string name)
        {
            Name = name;
        }

        protected DatasetInfo(IDataset dataset)
            : this(dataset.Name)
        {
            Description = dataset.Description;
            Tags = dataset.Tags?.ToList() ?? new List<string>();
            IsVisible = dataset.IsVisible;
        }
    }
}
