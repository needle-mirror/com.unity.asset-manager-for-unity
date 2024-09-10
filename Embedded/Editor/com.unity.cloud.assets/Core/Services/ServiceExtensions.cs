using System;

namespace Unity.Cloud.AssetsEmbedded
{
    static class ServiceExtensions
    {
        public static SearchRequestFilter From(this IAssetSearchFilter assetSearchFilter)
        {
            assetSearchFilter ??= new AssetSearchFilter();

            var anyQuery = assetSearchFilter.AccumulateAnyCriteria();

            return new SearchRequestFilter(assetSearchFilter.AccumulateIncludedCriteria(),
                assetSearchFilter.AccumulateExcludedCriteria(),
                anyQuery.criteria,
                anyQuery.criteria is {Count: > 0} ? anyQuery.minimumMatches : null,
                assetSearchFilter.Collections.GetValue());
        }
    }
}
