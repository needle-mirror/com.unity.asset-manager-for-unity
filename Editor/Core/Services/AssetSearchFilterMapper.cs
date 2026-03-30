using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Unity.Cloud.AssetsEmbedded;

namespace Unity.AssetManager.Core.Editor
{
    static class AssetSearchFilterMapper
    {
        static (DateTime startUtc, DateTime endUtc) GetUtcDayBoundaries(DateTime date)
        {
            var startOfDayLocal = DateTime.SpecifyKind(date.Date, DateTimeKind.Local);
            var startOfNextDayLocal = DateTime.SpecifyKind(date.Date.AddDays(1), DateTimeKind.Local);
            return (startOfDayLocal.ToUniversalTime(), startOfNextDayLocal.ToUniversalTime());
        }

        internal static IAssetSearchFilter Map(AssetSearchFilter assetSearchFilter, Func<IMetadata, MetadataValue> metadataMapper)
        {
            var cloudAssetSearchFilter = new Cloud.AssetsEmbedded.AssetSearchFilter();
            var minimumAnyRequirement = 0;

            if (assetSearchFilter.CreatedBy != null && assetSearchFilter.CreatedBy.Any())
            {
                cloudAssetSearchFilter.Include().AuthoringInfo.CreatedBy.WithValue(string.Join(" ", assetSearchFilter.CreatedBy));
            }

            if (assetSearchFilter.UpdatedBy != null && assetSearchFilter.UpdatedBy.Any())
            {
                cloudAssetSearchFilter.Include().AuthoringInfo.UpdatedBy.WithValue(string.Join(" ", assetSearchFilter.UpdatedBy));
            }

            if (assetSearchFilter.CreatedAtIncluded.HasValue)
            {
                var (startUtc, endUtc) = GetUtcDayBoundaries(assetSearchFilter.CreatedAtIncluded.Value);
                cloudAssetSearchFilter.Include().AuthoringInfo.Created.WithValueGreaterThanOrEqualTo(startUtc);
                cloudAssetSearchFilter.Include().AuthoringInfo.Created.WithValueLessThan(endUtc);
            }

            if (assetSearchFilter.CreatedAtExcluded.HasValue)
            {
                var (startUtc, endUtc) = GetUtcDayBoundaries(assetSearchFilter.CreatedAtExcluded.Value);
                cloudAssetSearchFilter.Exclude().AuthoringInfo.Created.WithValueGreaterThanOrEqualTo(startUtc);
                cloudAssetSearchFilter.Exclude().AuthoringInfo.Created.WithValueLessThan(endUtc);
            }

            if (assetSearchFilter.UpdatedAtIncluded.HasValue)
            {
                var (startUtc, endUtc) = GetUtcDayBoundaries(assetSearchFilter.UpdatedAtIncluded.Value);
                cloudAssetSearchFilter.Include().AuthoringInfo.Updated.WithValueGreaterThanOrEqualTo(startUtc);
                cloudAssetSearchFilter.Include().AuthoringInfo.Updated.WithValueLessThan(endUtc);
            }

            if (assetSearchFilter.UpdatedAtExcluded.HasValue)
            {
                var (startUtc, endUtc) = GetUtcDayBoundaries(assetSearchFilter.UpdatedAtExcluded.Value);
                cloudAssetSearchFilter.Exclude().AuthoringInfo.Updated.WithValueGreaterThanOrEqualTo(startUtc);
                cloudAssetSearchFilter.Exclude().AuthoringInfo.Updated.WithValueLessThan(endUtc);
            }

            if (assetSearchFilter.Status != null && assetSearchFilter.Status.Any())
            {
                cloudAssetSearchFilter.Include().Status.WithValue(string.Join(" ", assetSearchFilter.Status));
            }

            if (assetSearchFilter.NameFilter.Included.Any())
            {
                var stringPredicate = BuildStringPredicate(assetSearchFilter.NameFilter.Included, assetSearchFilter.NameFilter.UseAndLogic);
                cloudAssetSearchFilter.Include().Name.WithValue(stringPredicate);
            }

            if (assetSearchFilter.NameFilter.Excluded.Any())
            {
                var stringPredicate = BuildStringPredicate(assetSearchFilter.NameFilter.Excluded, assetSearchFilter.NameFilter.UseAndLogic);
                cloudAssetSearchFilter.Exclude().Name.WithValue(stringPredicate);
            }

            if (assetSearchFilter.DescriptionFilter.Included.Any())
            {
                var stringPredicate = BuildStringPredicate(assetSearchFilter.DescriptionFilter.Included, assetSearchFilter.DescriptionFilter.UseAndLogic);
                cloudAssetSearchFilter.Include().Description.WithValue(stringPredicate);
            }

            if (assetSearchFilter.DescriptionFilter.Excluded.Any())
            {
                var stringPredicate = BuildStringPredicate(assetSearchFilter.DescriptionFilter.Excluded, assetSearchFilter.DescriptionFilter.UseAndLogic);
                cloudAssetSearchFilter.Exclude().Description.WithValue(stringPredicate);
            }

            if (assetSearchFilter.AssetTypes != null && assetSearchFilter.AssetTypes.Any())
            {
                var assetTypes = assetSearchFilter.AssetTypes
                    .Select(AssetsSdkProvider.Map)
                    .ToArray();

                if (assetTypes.Length > 0)
                {
                    cloudAssetSearchFilter.Include().Type.WithValue(assetTypes);
                }
            }
            else if (assetSearchFilter.AssetTypeStrings != null && assetSearchFilter.AssetTypeStrings.Any())
            {
                var assetTypes = new List<Cloud.AssetsEmbedded.AssetType>();
                foreach (var typeString in assetSearchFilter.AssetTypeStrings)
                {
                    if (typeString.TryGetAssetTypeFromString(out var assetType))
                    {
                        assetTypes.Add(assetType);
                    }
                }

                if (assetTypes.Count > 0)
                {
                    cloudAssetSearchFilter.Include().Type.WithValue(assetTypes.ToArray());
                }
            }

            if (assetSearchFilter.Extensions != null && assetSearchFilter.Extensions.Any())
            {
                ParseFileExtensions(assetSearchFilter.Extensions, cloudAssetSearchFilter);
            }

            if (assetSearchFilter.TagsFilter.Included.Any())
            {
                cloudAssetSearchFilter.Include().Tags.WithValue(string.Join(" ", assetSearchFilter.TagsFilter.Included));
            }

            if (assetSearchFilter.TagsFilter.Excluded.Any())
            {
                cloudAssetSearchFilter.Exclude().Tags.WithValue(string.Join(" ", assetSearchFilter.TagsFilter.Excluded));
            }

            if (assetSearchFilter.Labels != null && assetSearchFilter.Labels.Any())
            {
                cloudAssetSearchFilter.Include().Labels.WithValue(string.Join(" ",assetSearchFilter.Labels));
            }

            if (assetSearchFilter.CustomMetadata != null)
            {
                foreach (var metadataGroup in assetSearchFilter.CustomMetadata.GroupBy(m => m.FieldKey))
                {
                    var metadataList = metadataGroup.ToList();
                    if (!metadataList.Any())
                        continue;

                    if (metadataList[0].Type == MetadataFieldType.Timestamp)
                    {
                        var minValue = metadataList.Min(m => ((TimestampMetadata) m).Value.DateTime);
                        var maxValue = metadataList.Max(m => ((TimestampMetadata) m).Value.DateTime);
                        cloudAssetSearchFilter.Include().Metadata.WithTimestampValue(metadataGroup.Key, minValue, true, maxValue);
                    }
                    else
                    {
                        var metadataValue = metadataList.Select(metadataMapper).FirstOrDefault(x => x != null);
                        if (metadataValue == null)
                            continue;

                        // Strings should be searched by predicate
                        if (metadataValue is Cloud.AssetsEmbedded.StringMetadata stringMetadata)
                        {
                            var stringPredicate = new StringPredicate(stringMetadata.Value, assetSearchFilter.IsExactMatchSearch
                                ? StringSearchOption.ExactMatch
                                : StringSearchOption.Prefix);
                            cloudAssetSearchFilter.Include().Metadata.WithTextValue(metadataGroup.Key, stringPredicate);
                        }

                        // Urls should be searched by label or, when no label defined, by the URL itself
                        else if (metadataValue is Cloud.AssetsEmbedded.UrlMetadata urlMetadata)
                        {
                            if (!string.IsNullOrEmpty(urlMetadata.Label))
                            {
                                var stringPredicate = new StringPredicate($"[{urlMetadata.Label}]", StringSearchOption.Prefix);
                                cloudAssetSearchFilter.Include().Metadata.WithTextValue(metadataGroup.Key, stringPredicate);
                            }
                            else if (urlMetadata.Uri != null)
                            {
                                cloudAssetSearchFilter.Include().Metadata.WithValue(metadataGroup.Key, urlMetadata);
                            }
                        }

                        // Multiselection values need to be searched by Any() to perform OR logical search between values
                        else if (metadataValue is Cloud.AssetsEmbedded.MultiSelectionMetadata multiSelectionMetadata)
                        {
                            cloudAssetSearchFilter.Any().Metadata.WithValue(metadataGroup.Key, multiSelectionMetadata);
                            ++minimumAnyRequirement;
                        }

                        // All other metadata are by exact match.
                        else
                        {
                            cloudAssetSearchFilter.Include().Metadata.WithValue(metadataGroup.Key, metadataValue);
                        }
                    }
                }
            }

            if (assetSearchFilter.Collection != null)
            {
                var collectionPaths = assetSearchFilter.Collection
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(x => new CollectionPath(x));
                cloudAssetSearchFilter.Collections.WhereContains(collectionPaths);
            }

            if (assetSearchFilter.Searches is {Count: > 0})
            {
                var fileExtensions = assetSearchFilter.Searches.Where(x => x.StartsWith('.')).ToList();
                ParseFileExtensions(fileExtensions, cloudAssetSearchFilter);

                var searches = assetSearchFilter.Searches.Where(x => !fileExtensions.Contains(x)).ToList();
                if (TryParseSearchTerms(searches, cloudAssetSearchFilter))
                {
                    ++minimumAnyRequirement; // We need to search to match in at least one field
                }
            }

            if (assetSearchFilter.AssetIds is {Count: > 0})
            {
                var searchString = string.Join(' ', assetSearchFilter.AssetIds);
                cloudAssetSearchFilter.Include().Id.WithValue(searchString);
            }

            if (assetSearchFilter.AssetVersions is {Count: > 0})
            {
                var searchString = string.Join(' ', assetSearchFilter.AssetVersions.Select(OptimizeVersionForSearch));
                cloudAssetSearchFilter.Any().Version.WithValue(searchString);
                cloudAssetSearchFilter.Any().Labels.WithValue("*");
                ++minimumAnyRequirement;
            }

            cloudAssetSearchFilter.Any().WhereMinimumMatchEquals(Math.Max(1, minimumAnyRequirement));

            return cloudAssetSearchFilter;
        }

        static StringPredicate BuildStringPredicate(List<string> values, bool useAndLogic)
        {
            var predicate = new StringPredicate(values[0], StringSearchOption.Wildcard);
            for (var i = 1; i < values.Count; ++i)
            {
                predicate = useAndLogic
                    ? predicate.And(values[i], StringSearchOption.Wildcard)
                    : predicate.Or(values[i], StringSearchOption.Wildcard);
            }
            return predicate;
        }

        static string OptimizeVersionForSearch(string version)
        {
            // Because of how elastic search tokenizes strings, we need to manipulate the version to minimize false positive results
            // We will therefore keep only that last component of the version string
            return version.Split('-')[^1];
        }

        static void ParseFileExtensions(List<string> fileExtensions, Cloud.AssetsEmbedded.AssetSearchFilter cloudAssetSearchFilter)
        {
            if (fileExtensions == null || !fileExtensions.Any())
            {
                return;
            }

            var pattern = new StringBuilder(fileExtensions[0]);
            for (var i = 1; i < fileExtensions.Count; ++i)
            {
                pattern.Append($"|{fileExtensions[i]}");
            }

            cloudAssetSearchFilter.Include().Files.Path.WithValue(new Regex($".*({pattern})", RegexOptions.IgnoreCase));
        }

        static bool TryParseSearchTerms(List<string> searchTerms, Cloud.AssetsEmbedded.AssetSearchFilter cloudAssetSearchFilter)
        {
            if (searchTerms == null || !searchTerms.Any())
            {
                return false;
            }

            // Search Name and Description by predicate, any term in the list
            var stringPredicate = new StringPredicate(searchTerms[0], StringSearchOption.Wildcard);
            for (var i = 1; i < searchTerms.Count; ++i)
            {
                stringPredicate = stringPredicate.Or(searchTerms[i], StringSearchOption.Wildcard);
            }

            cloudAssetSearchFilter.Any().Name.WithValue(stringPredicate);
            cloudAssetSearchFilter.Any().Description.WithValue(stringPredicate);

            // Search Tags by list
            cloudAssetSearchFilter.Any().Tags.WithValue(searchTerms);

            return true;
        }
    }
}
