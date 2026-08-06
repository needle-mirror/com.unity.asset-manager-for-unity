using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// A class that builds and executes a query to return a set of trashed assets.
    /// </summary>
    class TrashedAssetQueryBuilder
    {
        readonly IAssetDataSource m_AssetDataSource;
        readonly CacheConfigurationWrapper m_CacheConfiguration;
        readonly OrganizationId m_OrganizationId = OrganizationId.None;
        readonly List<ProjectId> m_ProjectIds = new();

        IAssetSearchFilter m_AssetSearchFilter;
        Range m_Range = Range.All;
        string m_SortingField = "name";
        SortingOrder m_SortingOrder = SortingOrder.Ascending;

        TrashedAssetQueryBuilder(IAssetDataSource assetDataSource, AssetRepositoryCacheConfiguration defaultCacheConfiguration)
        {
            m_AssetDataSource = assetDataSource;
            m_CacheConfiguration = new CacheConfigurationWrapper(defaultCacheConfiguration);
        }

        internal TrashedAssetQueryBuilder(IAssetDataSource assetDataSource, AssetRepositoryCacheConfiguration defaultCacheConfiguration, OrganizationId organizationId)
            : this(assetDataSource, defaultCacheConfiguration)
        {
            m_OrganizationId = organizationId;
        }

        internal TrashedAssetQueryBuilder(IAssetDataSource assetDataSource, AssetRepositoryCacheConfiguration defaultCacheConfiguration, ProjectDescriptor projectDescriptor)
            : this(assetDataSource, defaultCacheConfiguration)
        {
            m_OrganizationId = projectDescriptor.OrganizationId;
            m_ProjectIds.Add(projectDescriptor.ProjectId);
        }

        internal TrashedAssetQueryBuilder(IAssetDataSource assetDataSource, AssetRepositoryCacheConfiguration defaultCacheConfiguration, IEnumerable<ProjectDescriptor> projectDescriptors)
            : this(assetDataSource, defaultCacheConfiguration)
        {
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
        /// Sets an override to the default cache configuration for assets.
        /// </summary>
        /// <param name="assetCacheConfiguration">The configuration to apply when populating the assets. </param>
        /// <returns>The calling <see cref="TrashedAssetQueryBuilder"/>. </returns>
        public TrashedAssetQueryBuilder WithCacheConfiguration(AssetCacheConfiguration assetCacheConfiguration)
        {
            m_CacheConfiguration.SetAssetConfiguration(assetCacheConfiguration);
            return this;
        }

        /// <summary>
        /// Sets the filter to be used when querying assets.
        /// </summary>
        /// <param name="assetSearchFilter">The query filter. </param>
        /// <returns>The calling <see cref="TrashedAssetQueryBuilder"/>. </returns>
        public TrashedAssetQueryBuilder SelectWhereMatchesFilter(IAssetSearchFilter assetSearchFilter)
        {
            m_AssetSearchFilter = assetSearchFilter;
            return this;
        }

        /// <summary>
        /// Sets the order in which the results will be returned.
        /// </summary>
        /// <param name="sortingField">The field by which to sort the results. </param>
        /// <param name="sortingOrder">The sorting order (Ascending|Descending). </param>
        /// <returns>The calling <see cref="TrashedAssetQueryBuilder"/>. </returns>
        public TrashedAssetQueryBuilder OrderBy(string sortingField, SortingOrder sortingOrder = SortingOrder.Ascending)
        {
            m_SortingField = sortingField;
            m_SortingOrder = sortingOrder;
            return this;
        }

        /// <summary>
        /// Sets the range of results to return.
        /// </summary>
        /// <param name="range">The range of results. </param>
        /// <returns>The calling <see cref="TrashedAssetQueryBuilder"/>. </returns>
        public TrashedAssetQueryBuilder LimitTo(Range range)
        {
            m_Range = range;
            return this;
        }

        /// <summary>
        /// Executes the query and returns the trashed assets that satisfy the criteria.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>An async enumeration of <see cref="ITrashedAsset"/>. </returns>
        public async IAsyncEnumerable<ITrashedAsset> ExecuteAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var includeFields = m_CacheConfiguration.GetAssetFieldsFilter();
            var pagination = new SearchRequestPagination(m_SortingField, m_SortingOrder);
            var projectIds = m_ProjectIds.ToArray();

            if (projectIds.Length == 1)
            {
                var parameters = new SearchRequestParameters(includeFields)
                {
                    Filter = m_AssetSearchFilter?.From(),
                    Pagination = pagination,
                    PaginationRange = m_Range
                };
                var descriptor = new ProjectDescriptor(m_OrganizationId, projectIds[0]);
                var enumerator = m_AssetDataSource.ListAssetsInTrashAsync(descriptor, parameters, cancellationToken);

                await foreach (var assetData in enumerator)
                {
                    yield return ToTrashedAsset(assetData, descriptor, includeFields, m_AssetDataSource);
                }
            }
            else
            {
                var parameters = new AcrossProjectsSearchRequestParameters(projectIds, includeFields)
                {
                    Filter = m_AssetSearchFilter?.From(),
                    Pagination = pagination,
                    PaginationRange = m_Range
                };
                var enumerator = m_AssetDataSource.ListAssetsInTrashAcrossProjectsAsync(m_OrganizationId, projectIds, parameters, cancellationToken);

                await foreach (var assetData in enumerator)
                {
                    var projectDescriptor = new ProjectDescriptor(m_OrganizationId, assetData.SourceProjectId);
                    yield return ToTrashedAsset(assetData, projectDescriptor, includeFields, m_AssetDataSource);
                }
            }
        }

        static ITrashedAsset ToTrashedAsset(IAssetData assetData, ProjectDescriptor projectDescriptor, FieldsFilter includeFields, IAssetDataSource dataSource)
        {
            var assetDescriptor = new AssetDescriptor(projectDescriptor, assetData.Id, assetData.Version);
            var properties = assetData.From(assetDescriptor, includeFields);
            var trashDetails = assetData.TrashDetails?.Select(t => t.From()).ToArray() ?? Array.Empty<TrashDetails>();
            return new TrashedAssetEntity(assetDescriptor, properties, dataSource) { TrashDetails = trashDetails };
        }
    }
}
