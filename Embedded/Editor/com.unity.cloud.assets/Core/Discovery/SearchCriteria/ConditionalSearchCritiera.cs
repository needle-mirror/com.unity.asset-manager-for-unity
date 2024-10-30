using System;
using System.Collections.Generic;

namespace Unity.Cloud.AssetsEmbedded
{
    sealed class ConditionalSearchCriteria<T> : BaseSearchCriteria
    {
        readonly SearchConditionData m_Included;

        internal ConditionalSearchCriteria(string propertyName, string searchKey, string type)
            : base(propertyName, searchKey)
        {
            m_Included = new SearchConditionData(type);
        }

        internal override void Include(Dictionary<string, object> includedValues, string prefix = "")
        {
            m_Included.Validate();
            if (m_Included.Conditions.Count > 0)
            {
                includedValues.Add(SearchKey.BuildSearchKey(prefix), m_Included);
            }
        }

        /// <inheritdoc/>
        public override void Clear()
        {
            m_Included.Conditions.Clear();
        }

        /// <summary>
        /// Sets the value of the conditional criteria.
        /// </summary>
        /// <param name="range">The range to consider. </param>
        /// <param name="value">The threshold value. </param>
        public void WithValue(SearchConditionRange range, T value)
        {
            m_Included.AddCondition(new SearchConditionValue(range, value));
        }

        /// <summary>
        /// Sets the value of the conditional criteria.
        /// </summary>
        /// <param name="value">The threshold value. </param>
        public void WithValueGreaterThan(T value)
        {
            WithValue(SearchConditionRange.GreaterThan, value);
        }

        /// <inheritdoc cref="WithValueGreaterThan"/>
        public void WithValueGreaterThanOrEqualTo(T value)
        {
            WithValue(SearchConditionRange.GreaterThanOrEqual, value);
        }

        /// <inheritdoc cref="WithValueGreaterThan"/>
        public void WithValueLessThan(T value)
        {
            WithValue(SearchConditionRange.LessThan, value);
        }

        /// <inheritdoc cref="WithValueGreaterThan"/>
        public void WithValueLessThanOrEqualTo(T value)
        {
            WithValue(SearchConditionRange.LessThanOrEqual, value);
        }
    }
}
