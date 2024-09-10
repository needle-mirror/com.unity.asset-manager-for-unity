using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    static partial class EntityMapper
    {
        internal static void MapFrom(this TransformationEntity transformation, ITransformationData data)
        {
            transformation.OutputDatasetId = data.OutputDatasetId;
            transformation.LinkDatasetId = data.LinkDatasetId;
            transformation.InputFiles = data.InputFiles;
            transformation.WorkflowType = WorkflowTypeUtilities.FromJsonValue(data.WorkflowType);
            transformation.Status = data.Status;
            transformation.ErrorMessage = data.ErrorMessage;
            transformation.Progress = data.Progress;
            transformation.CreatedOn = data.CreatedOn;
            transformation.UpdatedAt = data.UpdatedAt;
            transformation.StartedAt = data.StartedAt;
            transformation.UserId = data.UserId;
            transformation.JobId = data.JobId;
        }

        static ITransformation From(this ITransformationData data, IAssetDataSource assetDataSource, TransformationDescriptor transformationDescriptor)
        {
            var transformation = new TransformationEntity(assetDataSource, transformationDescriptor);
            transformation.MapFrom(data);
            return transformation;
        }

        internal static ITransformation From(this ITransformationData data, IAssetDataSource assetDataSource, ProjectDescriptor projectDescriptor)
        {
            var assetDescriptor = new AssetDescriptor(projectDescriptor, data.AssetId, data.AssetVersion);
            var datasetDescriptor = new DatasetDescriptor(assetDescriptor, data.InputDatasetId);
            return data.From(assetDataSource, new TransformationDescriptor(datasetDescriptor, data.Id));
        }
    }
}
