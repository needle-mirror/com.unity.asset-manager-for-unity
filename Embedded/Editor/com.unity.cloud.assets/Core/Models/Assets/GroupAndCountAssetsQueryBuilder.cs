using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// A class that builds and executes a query to return an asset count.
    /// </summary>
    class GroupAndCountAssetsQueryBuilder
    {
        readonly IAssetDataSource m_AssetDataSource;
        readonly OrganizationId m_OrganizationId;
        readonly List<ProjectId> m_ProjectIds = new();

        IAssetSearchFilter m_AssetSearchFilter;
        int? m_Limit;

        GroupAndCountAssetsQueryBuilder(IAssetDataSource assetDataSource)
        {
            m_AssetDataSource = assetDataSource;
        }

        internal GroupAndCountAssetsQueryBuilder(IAssetDataSource assetDataSource, ProjectDescriptor projectDescriptor)
            : this(assetDataSource)
        {
            m_OrganizationId = projectDescriptor.OrganizationId;
            m_ProjectIds.Add(projectDescriptor.ProjectId);
        }

        internal GroupAndCountAssetsQueryBuilder(IAssetDataSource assetDataSource, IEnumerable<ProjectDescriptor> projectDescriptors)
            : this(assetDataSource)
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

        internal GroupAndCountAssetsQueryBuilder(IAssetDataSource assetDataSource, OrganizationId organizationId)
            : this(assetDataSource)
        {
            m_OrganizationId = organizationId;
        }

        /// <summary>
        /// Sets the filter to be used when querying assets.
        /// </summary>
        /// <param name="assetSearchFilter">The query filter. </param>
        /// <returns>The calling <see cref="GroupAndCountAssetsQueryBuilder"/>. </returns>
        public GroupAndCountAssetsQueryBuilder SelectWhereMatchesFilter(IAssetSearchFilter assetSearchFilter)
        {
            m_AssetSearchFilter = assetSearchFilter;
            return this;
        }

        /// <summary>
        /// Sets the limit of the counters.
        /// </summary>
        /// <param name="limit">The max count to return for each value. </param>
        /// <returns>The calling <see cref="GroupAndCountAssetsQueryBuilder"/>. </returns>
        public GroupAndCountAssetsQueryBuilder LimitTo(int limit)
        {
            m_Limit = limit;
            return this;
        }

        /// <summary>
        /// Executes the query and returns the result.
        /// </summary>
        /// <param name="groupBy">The field by which to group the assets. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is a dictionary of groups and their counts. </returns>
        public async Task<IReadOnlyDictionary<string, int>> ExecuteAsync(GroupableField groupBy, CancellationToken cancellationToken)
        {
            var aggregationFieldString = IsolatedSerialization.SerializeWithConverters(groupBy, IsolatedSerialization.StringEnumConverter);

            // Serialization adds quotes around the string, so we need to remove them.
            aggregationFieldString = aggregationFieldString.Trim('"');

            var projectIds = m_ProjectIds.ToArray();

            AggregateDto[] aggregations;

            switch (projectIds.Length)
            {
                case 1:
                {
                    var parameters = new SearchAndAggregateRequestParameters(aggregationFieldString)
                    {
                        Filter = m_AssetSearchFilter?.From(),
                        MaximumNumberOfItems = m_Limit,
                    };
                    var descriptor = new ProjectDescriptor(m_OrganizationId, projectIds[0]);
                    aggregations = await m_AssetDataSource.GetAssetAggregateAsync(descriptor, parameters, cancellationToken);
                    break;
                }
                default:
                {
                    var parameters = new AcrossProjectsSearchAndAggregateRequestParameters(projectIds, aggregationFieldString)
                    {
                        Filter = m_AssetSearchFilter?.From(),
                        MaximumNumberOfItems = m_Limit,
                    };
                    aggregations = await m_AssetDataSource.GetAssetAggregateAsync(m_OrganizationId, parameters, cancellationToken);
                    break;
                }
            }

            var data = new Dictionary<string, int>();
            for (var i = 0; i < aggregations.Length; ++i)
            {
                data.TryAdd(GetKeyAsString(aggregations[i].Value), aggregations[i].Count);
            }

            return data;
        }

        /// <summary>
        /// Executes the query and returns the result.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task whose result is a dictionary of . </returns>
        public async Task<IReadOnlyDictionary<CollectionDescriptor, int>> GroupByCollectionAndExecuteAsync(CancellationToken cancellationToken)
        {
            const string collectionGroup = "collections";

            var projectIds = m_ProjectIds.ToArray();

            AggregateDto[] aggregations;

            switch (projectIds.Length)
            {
                case 1:
                {
                    var parameters = new SearchAndAggregateRequestParameters(collectionGroup)
                    {
                        Filter = m_AssetSearchFilter?.From(),
                        MaximumNumberOfItems = m_Limit,
                    };
                    var descriptor = new ProjectDescriptor(m_OrganizationId, projectIds[0]);
                    aggregations = await m_AssetDataSource.GetAssetAggregateAsync(descriptor, parameters, cancellationToken);
                    break;
                }
                default:
                {
                    var parameters = new AcrossProjectsSearchAndAggregateRequestParameters(projectIds, collectionGroup)
                    {
                        Filter = m_AssetSearchFilter?.From(),
                        MaximumNumberOfItems = m_Limit,
                    };
                    aggregations = await m_AssetDataSource.GetAssetAggregateAsync(m_OrganizationId, parameters, cancellationToken);
                    break;
                }
            }

            var data = new Dictionary<CollectionDescriptor, int>();
            for (var i = 0; i < aggregations.Length; ++i)
            {
                data.TryAdd(GetKeyAsCollectionDescriptor(aggregations[i].Value), aggregations[i].Count);
            }

            return data;
        }

        CollectionDescriptor GetKeyAsCollectionDescriptor(object key)
        {
            var split = GetKeyAsString(key).Split('/');
            if (split.Length >= 2)
            {
                var projectIdStr = split[0];
                projectIdStr = projectIdStr.Replace("proj-", "");

                var collectionPath = CollectionPath.BuildPath(split.Skip(1).ToArray());
                return new CollectionDescriptor(new ProjectDescriptor(m_OrganizationId, new ProjectId(projectIdStr)), collectionPath);
            }

            throw new ArgumentException("The key is not a collection descriptor.");
        }



        static string GetKeyAsString(object key)
        {
            return Uri.UnescapeDataString(key?.ToString() ?? "");
        }
    }
}
