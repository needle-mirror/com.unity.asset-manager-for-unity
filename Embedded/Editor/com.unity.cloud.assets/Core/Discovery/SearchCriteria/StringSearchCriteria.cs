using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// Exposes partial string searches.
    /// </summary>
    class StringSearchCriteria : SearchCriteria<string>
    {
        [Flags]
        public enum SearchOptions
        {
            None = 0,
            Prefix = 2
        }

        [DataContract]
        internal struct PartialQuery
        {
            [DataMember(Name = "type")]
            public string Type { get; set; }

            [DataMember(Name = "value")]
            public string Value { get; set; }

            public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
        }

        internal static readonly char[] k_WildcardChars = new[] {'*', '?'};

        PartialQuery m_IncludedPartial;

        internal StringSearchCriteria(string propertyName, string searchKey)
            : base(propertyName, searchKey) { }

        /// <inheritdoc/>
        internal override void Include(Dictionary<string, object> includedValues, string prefix = "")
        {
            if (!m_IncludedPartial.IsEmpty)
            {
                includedValues.Add(SearchKey.BuildSearchKey(prefix), m_IncludedPartial);
                return;
            }

            base.Include(includedValues, prefix);
        }

        public override void Clear()
        {
            base.Clear();

            m_IncludedPartial = default;
        }

        /// <inheritdoc />
        public override void WithValue(string value)
        {
            if (k_WildcardChars.Any(value.Contains))
            {
                WithValue(value, SearchOptions.None);
                return;
            }

            base.WithValue(value);
            m_IncludedPartial = default;
        }

        public void WithValue(string value, SearchOptions options)
        {
            m_IncludedPartial = options.HasFlag(SearchOptions.Prefix) ? BuildPrefixQuery(value) : BuildWildcardQuery(value);
            m_Included = null;
        }

        public void WithValue(Regex pattern)
        {
            m_IncludedPartial = BuildRegexQuery(pattern);
            m_Included = null;
        }

        public void WithFuzzyValue(string value)
        {
            m_IncludedPartial = BuildFuzzyQuery(value);
            m_Included = null;
        }

        internal static PartialQuery BuildPrefixQuery(string value)
        {
            return new PartialQuery
            {
                Type = "prefix",
                Value = value,
            };
        }

        internal static PartialQuery BuildWildcardQuery(string value)
        {
            return new PartialQuery
            {
                Type = "wildcard",
                Value = value,
            };
        }

        internal static PartialQuery BuildRegexQuery(Regex regex)
        {
            return new PartialQuery
            {
                Type = "regex",
                Value = regex.ToString(),
            };
        }

        internal static PartialQuery BuildFuzzyQuery(string value)
        {
            return new PartialQuery
            {
                Type = "fuzzy",
                Value = value
            };
        }
    }
}
