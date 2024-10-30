using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// A class that builds and executes a query to return a set of field definitions.
    /// </summary>
    class FieldDefinitionQueryBuilder
    {
        readonly IAssetDataSource m_AssetDataSource;
        readonly OrganizationId m_OrganizationId;

        FieldDefinitionSearchFilter m_SearchFilter;
        Range m_Range = Range.All;
        SortingOrder m_SortingOrder = SortingOrder.Ascending;

        internal FieldDefinitionQueryBuilder(IAssetDataSource assetDataSource, OrganizationId organizationId)
        {
            m_AssetDataSource = assetDataSource;
            m_OrganizationId = organizationId;
        }

        /// <summary>
        /// Sets the filter to use for the query.
        /// </summary>
        /// <param name="searchFilter">The search criteria. </param>
        /// <returns>The calling <see cref="FieldDefinitionQueryBuilder"/>. </returns>
        public FieldDefinitionQueryBuilder SelectWhereMatchesFilter(FieldDefinitionSearchFilter searchFilter)
        {
            m_SearchFilter = searchFilter;
            return this;
        }

        /// <summary>
        /// Sets the order in which the results will be returned.
        /// </summary>
        /// <param name="sortingOrder">The sorting order (Ascending|Descending). </param>
        /// <returns>The calling <see cref="FieldDefinitionQueryBuilder"/>. </returns>
        public FieldDefinitionQueryBuilder OrderByName(SortingOrder sortingOrder )
        {
            m_SortingOrder = sortingOrder;
            return this;
        }

        /// <summary>
        /// Sets the range of results to return.
        /// </summary>
        /// <param name="range">The range of results to return. </param>
        /// <returns>The calling <see cref="FieldDefinitionQueryBuilder"/>. </returns>
        public FieldDefinitionQueryBuilder LimitTo(Range range)
        {
            m_Range = range;
            return this;
        }

        /// <summary>
        /// Executes the query and returns the results.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request.</param>
        /// <returns>An async enumeration of <see cref="IFieldDefinition"/>. </returns>
        public async IAsyncEnumerable<IFieldDefinition> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var pagination = new PaginationData
            {
                Range = m_Range,
                SortingOrder = m_SortingOrder
            };

            m_SearchFilter ??= new FieldDefinitionSearchFilter();

            var queryParameters = new Dictionary<string, string>()
            {
                { "IncludeDeleted", m_SearchFilter.Deleted.GetValue().ToString() }
            };
            var fieldOrigin = m_SearchFilter.FieldOrigin.GetValue();
            if (fieldOrigin.HasValue)
            {
                queryParameters.Add("FieldOrigin", fieldOrigin.ToString());
            }

            var enumerator = m_AssetDataSource.ListFieldDefinitionsAsync(m_OrganizationId, pagination, queryParameters, cancellationToken);
            await foreach(var fieldDefinitionData in enumerator)
            {
                yield return fieldDefinitionData.From(m_AssetDataSource, m_OrganizationId);
            }
        }
    }
}
