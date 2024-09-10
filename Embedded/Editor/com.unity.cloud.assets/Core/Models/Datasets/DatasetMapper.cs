using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    static partial class EntityMapper
    {
        internal static void MapFrom(this DatasetEntity dataset, IAssetDataSource assetDataSource, IDatasetData datasetData, DatasetFields includeFields)
        {
            dataset.Name = datasetData.Name;
            dataset.Tags = datasetData.Tags;
            dataset.SystemTags = datasetData.SystemTags;
            dataset.Status = datasetData.Status;
            dataset.IsVisible = datasetData.IsVisible ?? false;
            dataset.WorkflowName = datasetData.WorkflowName;

            if (includeFields.HasFlag(DatasetFields.description))
                dataset.Description = datasetData.Description;
            if (includeFields.HasFlag(DatasetFields.authoring))
                dataset.AuthoringInfo = new AuthoringInfo(datasetData.CreatedBy, datasetData.Created, datasetData.UpdatedBy, datasetData.Updated);
            if (includeFields.HasFlag(DatasetFields.metadata))
                dataset.MetadataEntity.Properties = datasetData.Metadata?.From(assetDataSource, dataset.Descriptor.OrganizationId);
            if (includeFields.HasFlag(DatasetFields.systemMetadata))
                dataset.SystemMetadataEntity.Properties = datasetData.SystemMetadata?.From();
            if (includeFields.HasFlag(DatasetFields.filesOrder))
                dataset.FileOrder = datasetData.FileOrder;
        }

        internal static DatasetEntity From(this IDatasetData datasetData, IAssetDataSource assetDataSource, AssetDescriptor assetDescriptor, DatasetFields includeFields)
        {
            return datasetData.From(assetDataSource, new DatasetDescriptor(assetDescriptor, datasetData.DatasetId), includeFields);
        }

        internal static DatasetEntity From(this IDatasetData datasetData, IAssetDataSource assetDataSource, DatasetDescriptor datasetDescriptor, DatasetFields includeFields)
        {
            var dataset = new DatasetEntity(assetDataSource, datasetDescriptor);
            dataset.MapFrom(assetDataSource, datasetData, includeFields);
            return dataset;
        }

        internal static DatasetData From(this DatasetEntity datasetEntity)
        {
            return new DatasetData
            {
                DatasetId = datasetEntity.Descriptor.DatasetId,
                Name = datasetEntity.Name,
                Description = datasetEntity.Description,
                CreatedBy = datasetEntity.AuthoringInfo?.CreatedBy.ToString(),
                Created = datasetEntity.AuthoringInfo?.Created,
                UpdatedBy = datasetEntity.AuthoringInfo?.UpdatedBy.ToString(),
                Updated = datasetEntity.AuthoringInfo?.Updated,
                FileOrder = datasetEntity.FileOrder,
                Metadata = datasetEntity.MetadataEntity?.From() ?? new Dictionary<string, object>(),
                Tags = datasetEntity.Tags?.ToList(),
                SystemTags = datasetEntity.SystemTags,
                Status = datasetEntity.Status,
                IsVisible = datasetEntity.IsVisible,
                WorkflowName = datasetEntity.WorkflowName,
            };
        }

        internal static IDatasetUpdateData From(this IDatasetUpdate dataset)
        {
            return new DatasetUpdateData
            {
                Name = dataset.Name,
                Description = dataset.Description,
                Tags = dataset.Tags,
                FileOrder = dataset.FileOrder,
                IsVisible = dataset.IsVisible,
            };
        }

        internal static IDatasetBaseData From(this IDatasetCreation dataset)
        {
            return new DatasetBaseData
            {
                Name = dataset.Name,
                Description = dataset.Description,
                Metadata = dataset.Metadata?.ToObjectDictionary() ?? new Dictionary<string, object>(),
                Tags = dataset.Tags ?? new List<string>(),// WORKAROUND until backend supports null metadata
            };
        }
    }
}
