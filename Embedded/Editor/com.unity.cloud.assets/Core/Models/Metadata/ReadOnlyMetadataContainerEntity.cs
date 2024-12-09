using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Cloud.AssetsEmbedded
{
    class ReadOnlyMetadataContainerEntity : IReadOnlyMetadataContainer
    {
        protected readonly IMetadataDataSource m_DataSource;

        protected Dictionary<string, MetadataObject> m_Properties;

        internal IDictionary<string, MetadataObject> Properties
        {
            get => m_Properties;
            set => m_Properties = value?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, MetadataObject>();
        }

        public ReadOnlyMetadataContainerEntity(IMetadataDataSource dataSource)
        {
            m_DataSource = dataSource;
        }

        /// <summary>
        /// Clears the cache.
        /// </summary>
        public void Refresh()
        {
            m_Properties = null;
        }

        /// <summary>
        /// Refreshes the metadata dictionary.
        /// </summary>
        /// <param name="keys">The subset of keys to include in the dictionary; if empty or null all keys will be included. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request.</param>
        /// <returns>A task with no result.</returns>
        public async Task<Dictionary<string, MetadataValue>> GetMetadataAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
        {
            var keyList = keys?.ToHashSet() ?? new HashSet<string>();

            if (m_Properties == null)
            {
                m_Properties = await m_DataSource.GetAsync(keyList, cancellationToken);

                m_Properties ??= new Dictionary<string, MetadataObject>();
            }

            var metadata = keyList.Count == 0 ? m_Properties : m_Properties.Where(kvp => keyList.Contains(kvp.Key));
            return metadata.ToDictionary(kvp => kvp.Key, kvp => (MetadataValue) kvp.Value);
        }

        /// <inheritdoc />
        public MetadataQueryBuilder Query()
        {
            return new MetadataQueryBuilder(this);
        }
    }
}
