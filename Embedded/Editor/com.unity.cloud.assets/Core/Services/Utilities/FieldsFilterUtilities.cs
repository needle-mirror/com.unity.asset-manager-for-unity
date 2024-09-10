using System;
using System.Collections.Generic;

namespace Unity.Cloud.AssetsEmbedded
{
    static class FieldsFilterUtilities
    {
        /// <summary>
        /// A list of fields that are optional in service requests, but will always be included when an asset is requested.
        /// </summary>
        static readonly string[] s_DefaultAssetFields =
        {
            "isFrozen",
            "versionNumber",
            "changelog",
            "parentAssetVersion",
            "parentVersionNumber",
        };

        public delegate void OnFieldFilterSelected(string field);

        internal static void Parse(this FieldsFilter fieldsFilter, OnFieldFilterSelected select)
        {
            if (fieldsFilter == null) return;

            if (fieldsFilter.AssetFields.HasFlag(AssetFields.all))
            {
                select("*");
            }
            else
            {
                foreach (var defaultField in s_DefaultAssetFields)
                {
                    select(defaultField);
                }
                fieldsFilter.AssetFields.Parse(select);
            }

            fieldsFilter.DatasetFields.Parse(select, "datasets.");
            fieldsFilter.FileFields.Parse(select, "files.");

            fieldsFilter.MetadataFields.Select(select, "metadata",
                fieldsFilter.AssetFields.HasFlag(AssetFields.metadata),
                fieldsFilter.DatasetFields.HasFlag(DatasetFields.metadata),
                fieldsFilter.FileFields.HasFlag(FileFields.metadata));
        }

        internal static void Parse(this FileFields fileFields, OnFieldFilterSelected select, string prefix = "")
        {
            if (fileFields.HasFlag(FileFields.all))
            {
                select($"{prefix}*");
                // Explicitly include these as they fail to be returned when using the wildcard.
                select($"{prefix}downloadURL");
                select($"{prefix}previewURL");
                return;
            }

            foreach (FileFields value in Enum.GetValues(typeof(FileFields)))
            {
                if (value is FileFields.all or FileFields.none) continue;
                if (fileFields.HasFlag(value))
                {
                    if (value == FileFields.authoring)
                    {
                        IncludeAuthoringFields(prefix, select);
                    }
                    else
                    {
                        select($"{prefix}{value.ToString()}");
                    }
                }
            }
        }

        internal static void Parse(this DatasetFields datasetFields, OnFieldFilterSelected select, string prefix = "")
        {
            if (datasetFields.HasFlag(DatasetFields.all))
            {
                select($"{prefix}*");
                return;
            }

            foreach (DatasetFields value in Enum.GetValues(typeof(DatasetFields)))
            {
                if (value is DatasetFields.all or DatasetFields.none or DatasetFields.files) continue;
                if (datasetFields.HasFlag(value))
                {
                    if (value == DatasetFields.authoring)
                    {
                        IncludeAuthoringFields(prefix, select);
                    }
                    else
                    {
                        select($"{prefix}{value.ToString()}");
                    }
                }
            }
        }

        static void Parse(this AssetFields assetFields, OnFieldFilterSelected select)
        {
            foreach (AssetFields value in Enum.GetValues(typeof(AssetFields)))
            {
                if (value == AssetFields.all || value == AssetFields.none) continue;
                if (assetFields.HasFlag(value))
                {
                    if (value == AssetFields.authoring)
                    {
                        IncludeAuthoringFields("", select);
                    }
                    else
                    {
                        select(value.ToString());
                    }
                }
            }
        }

        static void IncludeAuthoringFields(string prefix, OnFieldFilterSelected action)
        {
            action(prefix + "created");
            action(prefix + "createdBy");
            action(prefix + "updated");
            action(prefix + "updatedBy");
        }

        static void Select(this IEnumerable<string> metadataKeys, OnFieldFilterSelected select, string metadataprefix, bool hasAssetFlag, bool hasDatasetFlag, bool hasFileFlag)
        {
            foreach (var field in metadataKeys)
            {
                if (hasAssetFlag) select($"{metadataprefix}.{field}");
                if (hasDatasetFlag) select($"datasets.{metadataprefix}.{field}");
                if (hasFileFlag) select($"files.{metadataprefix}.{field}");
            }
        }
    }
}
