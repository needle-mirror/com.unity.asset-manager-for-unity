using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// A class that builds and executes a query to return a set of assets.
    /// </summary>
    class AssetQueryBuilder
    {
        readonly IAssetDataSource m_AssetDataSource;
        readonly OrganizationId m_OrganizationId;
        readonly List<ProjectId> m_ProjectIds = new();

        IAssetSearchFilter m_AssetSearchFilter;
        Range m_Range = Range.All;
        string m_SortingField = nameof(IAsset.Name);
        SortingOrder m_SortingOrder = SortingOrder.Ascending;

        internal AssetQueryBuilder(IAssetDataSource assetDataSource, ProjectDescriptor projectDescriptor)
        {
            m_AssetDataSource = assetDataSource;
            m_OrganizationId = projectDescriptor.OrganizationId;
            m_ProjectIds.Add(projectDescriptor.ProjectId);
        }

        internal AssetQueryBuilder(IAssetDataSource assetDataSource, OrganizationId organizationId)
        {
            m_AssetDataSource = assetDataSource;
            m_OrganizationId = organizationId;
        }

        internal AssetQueryBuilder(IAssetDataSource assetDataSource, IEnumerable<ProjectDescriptor> projectDescriptors)
        {
            m_AssetDataSource = assetDataSource;

            var projects = projectDescriptors.ToArray();
            if (projects.Length == 0)
            {
                throw new ArgumentNullException(nameof(projectDescriptors), "No project descriptors were provided.");
            }

            m_OrganizationId = projects[0].OrganizationId;
            for (var i = 1; i < projects.Length; i++)
            {
                if (projects[i].OrganizationId != m_OrganizationId)
                {
                    throw new InvalidOperationException("The projects do not belong to the same organization.");
                }
            }

            m_ProjectIds.AddRange(projects.Select(descriptor => descriptor.ProjectId));
        }

        /// <summary>
        /// Sets the filter to be used when querying assets.
        /// </summary>
        /// <param name="assetSearchFilter">The query filter. </param>
        /// <returns>The calling <see cref="AssetQueryBuilder"/>. </returns>
        public AssetQueryBuilder SelectWhereMatchesFilter(IAssetSearchFilter assetSearchFilter)
        {
            m_AssetSearchFilter = assetSearchFilter;
            return this;
        }

        /// <summary>
        /// Sets the order in which the results will be returned.
        /// </summary>
        /// <param name="sortingField">The field by which to sort the results. </param>
        /// <param name="sortingOrder">The sorting order (Ascending|Descending). </param>
        /// <returns>The calling <see cref="AssetQueryBuilder"/>. </returns>
        public AssetQueryBuilder OrderBy(string sortingField, SortingOrder sortingOrder = SortingOrder.Ascending)
        {
            m_SortingField = sortingField;
            m_SortingOrder = sortingOrder;
            return this;
        }

        /// <summary>
        /// Sets the range of results to return.
        /// </summary>
        /// <param name="range">The range of results. </param>
        /// <returns>The calling <see cref="AssetQueryBuilder"/>. </returns>
        public AssetQueryBuilder LimitTo(Range range)
        {
            m_Range = range;
            return this;
        }

        /// <summary>
        /// Executes the query and returns the assets with a default version that satisfy the critiera.
        /// </summary>
        /// <remarks>The default version of an asset is defined as the "Latest" frozen version, or if there are no frozen versions, the unfrozen "Pending" version.</remarks>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>An async enumeration of <see cref="IAsset"/>. </returns>
        public async IAsyncEnumerable<IAsset> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var includeFields = FieldsFilter.DefaultAssetIncludes;
            includeFields.AssetFields |= AssetFields.metadata | AssetFields.previewFileUrl;

            var pagination = new SearchRequestPagination(m_SortingField, m_SortingOrder);

            var projectIds = m_ProjectIds.ToArray();

            switch (projectIds.Length)
            {
                case 1:
                {
                    var parameters = new SearchRequestParameters(includeFields)
                    {
                        Filter = m_AssetSearchFilter?.From(),
                        Pagination = pagination,
                        PaginationRange = m_Range
                    };
                    var descriptor = new ProjectDescriptor(m_OrganizationId, projectIds[0]);
                    var enumerator = m_AssetDataSource.ListAssetsAsync(descriptor, parameters, cancellationToken);
                    await foreach (var assetData in enumerator)
                    {
                        yield return assetData.From(m_AssetDataSource, descriptor, includeFields);
                    }

                    break;
                }
                default:
                {
                    var parameters = new AcrossProjectsSearchRequestParameters(projectIds, includeFields)
                    {
                        Filter = m_AssetSearchFilter?.From(),
                        Pagination = pagination,
                        PaginationRange = m_Range
                    };
                    var enumerator = m_AssetDataSource.ListAssetsAsync(m_OrganizationId, projectIds, parameters, cancellationToken);
                    await foreach (var assetData in enumerator)
                    {
                        yield return assetData.From(m_AssetDataSource, m_OrganizationId, projectIds, includeFields);
                    }

                    break;
                }
            }
        }
    }
}
