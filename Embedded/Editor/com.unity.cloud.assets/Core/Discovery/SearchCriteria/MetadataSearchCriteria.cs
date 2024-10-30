using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Unity.Cloud.AssetsEmbedded
{
    using SearchOptions = StringSearchCriteria.SearchOptions;

    sealed class MetadataSearchCriteria : BaseSearchCriteria
    {
        readonly Dictionary<string, object> m_Included = new();

        public MetadataSearchCriteria(string propertyName, string searchKey)
            : base(propertyName, searchKey) { }

        /// <inheritdoc/>
        internal override void Include(Dictionary<string, object> includedValues, string prefix = "")
        {
            AddValues(m_Included, includedValues, prefix);
        }

        void AddValues(IDictionary<string, object> from, IDictionary<string, object> to, string prefix)
        {
            var searchKey = SearchKey.BuildSearchKey(prefix);
            foreach (var kvp in from)
            {
                to.Add($"{searchKey}.{kvp.Key}", kvp.Value);
            }
        }

        /// <inheritdoc/>
        public override void Clear()
        {
            m_Included.Clear();
        }

        /// <summary>
        /// Sets the value of the metadata field.
        /// </summary>
        /// <param name="metadataFieldKey">The key of the metadata field. </param>
        /// <param name="value">The expected value of the field. </param>
        public void WithValue(string metadataFieldKey, MetadataValue value)
        {
            if (value is StringMetadata stringMetadata)
            {
                WithValue(metadataFieldKey, stringMetadata.GetValue() as string, SearchOptions.None);
            }
            else
            {
                m_Included[metadataFieldKey] = value.GetValue();
            }
        }

        /// <summary>
        /// Sets the value of the metadata string field.
        /// </summary>
        /// <param name="metadataFieldKey">The key of the metadata field. </param>
        /// <param name="value">The expected value of the string field. </param>
        /// <param name="options">The search options. </param>
        public void WithValue(string metadataFieldKey, string value, SearchOptions options)
        {
            if (options.HasFlag(SearchOptions.Prefix))
            {
                m_Included[metadataFieldKey] = StringSearchCriteria.BuildPrefixQuery(value);
            }
            else if (StringSearchCriteria.k_WildcardChars.Any(value.Contains))
            {
                m_Included[metadataFieldKey] = StringSearchCriteria.BuildWildcardQuery(value);
            }
            else
            {
                m_Included[metadataFieldKey] = value;
            }
        }

        /// <summary>
        /// Sets the value of the metadata string field.
        /// </summary>
        /// <param name="metadataFieldKey">The key of the metadata field. </param>
        /// <param name="pattern">The expected pattern of the string field. </param>
        public void WithValue(string metadataFieldKey, Regex pattern)
        {
            m_Included[metadataFieldKey] = StringSearchCriteria.BuildRegexQuery(pattern);
        }

        /// <summary>
        /// Sets the value of the metadata string field.
        /// </summary>
        /// <param name="metadataFieldKey">The key of the metadata field. </param>
        /// <param name="value">The approximate value of the string field. </param>
        public void WithFuzzyValue(string metadataFieldKey, string value)
        {
            m_Included[metadataFieldKey] = StringSearchCriteria.BuildFuzzyQuery(value);
        }
    }
}
