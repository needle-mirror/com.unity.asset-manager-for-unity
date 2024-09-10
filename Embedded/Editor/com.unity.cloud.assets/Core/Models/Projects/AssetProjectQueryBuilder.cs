using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// A class that builds and executes a query to return a set of projects.
    /// </summary>
    class AssetProjectQueryBuilder
    {
        readonly IAssetDataSource m_AssetDataSource;
        readonly OrganizationId m_OrganizationId;

        Range m_Range = Range.All;

        internal AssetProjectQueryBuilder(IAssetDataSource assetDataSource, OrganizationId organizationId)
        {
            m_AssetDataSource = assetDataSource;
            m_OrganizationId = organizationId;
        }

        /// <summary>
        /// Sets the range of results to return.
        /// </summary>
        /// <param name="range">The range of results. </param>
        /// <returns>The calling <see cref="AssetProjectQueryBuilder"/>. </returns>
        public AssetProjectQueryBuilder LimitTo(Range range)
        {
            m_Range = range;
            return this;
        }

        /// <summary>
        /// Executes the query and returns the results.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>An async enumeration of <see cref="IAssetProject"/>. </returns>
        public async IAsyncEnumerable<IAssetProject> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var pagination = new PaginationData
            {
                Range = m_Range
            };
            var results = m_AssetDataSource.ListProjectsAsync(m_OrganizationId, pagination, cancellationToken);
            await foreach(var project in results)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return project.From(m_AssetDataSource, m_OrganizationId);
            }
        }
    }
}
