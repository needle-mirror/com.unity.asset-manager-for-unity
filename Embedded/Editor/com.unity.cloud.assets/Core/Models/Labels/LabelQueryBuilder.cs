using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// A class that builds and executes a query to return a set of labels.
    /// </summary>
    sealed class LabelQueryBuilder
    {
        readonly IAssetDataSource m_DataSource;
        readonly OrganizationId m_OrganizationId;

        LabelSearchFilter m_Filter;
        Range m_Range = Range.All;

        internal LabelQueryBuilder(IAssetDataSource dataSource, OrganizationId organizationId)
        {
            m_DataSource = dataSource;
            m_OrganizationId = organizationId;
        }

        /// <summary>
        /// Sets the filter to be used when querying labels.
        /// </summary>
        /// <param name="filter">The query filter. </param>
        /// <returns>The calling <see cref="LabelQueryBuilder"/>. </returns>
        public LabelQueryBuilder SelectWhereMatchesFilter(LabelSearchFilter filter)
        {
            m_Filter = filter;
            return this;
        }

        /// <summary>
        /// Sets the range of results to return.
        /// </summary>
        /// <param name="range">The range of results. </param>
        /// <returns>The calling <see cref="LabelQueryBuilder"/>. </returns>
        public LabelQueryBuilder LimitTo(Range range)
        {
            m_Range = range;
            return this;
        }

        /// <summary>
        /// Executes the query and returns the labels that satisfy the criteria.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>An async enumeration of <see cref="ILabel"/>. </returns>
        public async IAsyncEnumerable<ILabel> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            m_Filter ??= new LabelSearchFilter();

            var pagination = new PaginationData
            {
                Range = m_Range
            };

            var results = m_DataSource.ListLabelsAsync(m_OrganizationId,
                pagination,
                m_Filter.IsArchived.GetValue(),
                m_Filter.IsSystemLabel.GetValue(),
                cancellationToken);
            await foreach (var result in results)
            {
                yield return result.From(m_DataSource, m_OrganizationId);
            }
        }
    }
}
