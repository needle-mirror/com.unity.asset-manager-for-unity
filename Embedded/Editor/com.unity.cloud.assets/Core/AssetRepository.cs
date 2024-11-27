using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    /// <summary>
    /// Interface to transform user facing data like <see cref="IProjectData"/> into service DTOs.
    /// </summary>
    class AssetRepository : IAssetRepository
    {
        readonly IAssetDataSource m_DataSource;

        internal AssetRepository(IAssetDataSource dataSource)
        {
            m_DataSource = dataSource;
        }

        /// <inheritdoc />
        public AssetProjectQueryBuilder QueryAssetProjects(OrganizationId organizationId)
        {
            return new AssetProjectQueryBuilder(m_DataSource, organizationId);
        }

        /// <inheritdoc />
        public async Task<IAssetProject> GetAssetProjectAsync(ProjectDescriptor projectDescriptor, CancellationToken cancellationToken)
        {
            var projectData = await m_DataSource.GetProjectAsync(projectDescriptor, cancellationToken);
            return projectData.From(m_DataSource, projectDescriptor);
        }

        /// <inheritdoc />
        public async Task<IAssetProject> EnableProjectForAssetManagerAsync(ProjectDescriptor projectDescriptor, CancellationToken cancellationToken)
        {
            var projectData = await m_DataSource.EnableProjectAsync(projectDescriptor, cancellationToken);
            return projectData.From(m_DataSource, projectDescriptor);
        }

        /// <inheritdoc />
        public async Task<IAssetProject> CreateAssetProjectAsync(OrganizationId organizationId, IAssetProjectCreation projectCreation, CancellationToken cancellationToken)
        {
            var data = new ProjectBaseData
            {
                Name = projectCreation.Name,
                Metadata = projectCreation.Metadata
            };
            var projectData = await m_DataSource.CreateProjectAsync(organizationId, data, cancellationToken);
            return projectData.From(m_DataSource, organizationId);
        }

        /// <inheritdoc />
        public async Task<IAssetCollection> GetAssetCollectionAsync(CollectionDescriptor collectionDescriptor, CancellationToken cancellationToken)
        {
            var collectionData = await m_DataSource.GetCollectionAsync(collectionDescriptor, cancellationToken);
            return collectionData.From(m_DataSource, collectionDescriptor);
        }

        /// <inheritdoc />
        public AssetQueryBuilder QueryAssets(IEnumerable<ProjectDescriptor> projectDescriptors)
        {
            return new AssetQueryBuilder(m_DataSource, projectDescriptors);
        }

        /// <inheritdoc />
        public AssetQueryBuilder QueryAssets(OrganizationId organizationId)
        {
            return new AssetQueryBuilder(m_DataSource, organizationId);
        }

        /// <inheritdoc />
        public GroupAndCountAssetsQueryBuilder GroupAndCountAssets(IEnumerable<ProjectDescriptor> projectDescriptors)
        {
            return new GroupAndCountAssetsQueryBuilder(m_DataSource, projectDescriptors);
        }

        /// <inheritdoc />
        public GroupAndCountAssetsQueryBuilder GroupAndCountAssets(OrganizationId organizationId)
        {
            return new GroupAndCountAssetsQueryBuilder(m_DataSource, organizationId);
        }

        /// <inheritdoc />
        public async Task<IAsset> GetAssetAsync(AssetDescriptor assetDescriptor, CancellationToken cancellationToken)
        {
            var assetData = await m_DataSource.GetAssetAsync(assetDescriptor, FieldsFilter.DefaultAssetIncludes, cancellationToken);
            return assetData.From(m_DataSource, assetDescriptor, FieldsFilter.DefaultAssetIncludes);
        }

        /// <inheritdoc />
        public async Task<IAsset> GetAssetAsync(ProjectDescriptor projectDescriptor, AssetId assetId, string label, CancellationToken cancellationToken)
        {
            var assetData = await m_DataSource.GetAssetAsync(projectDescriptor, assetId, label, FieldsFilter.DefaultAssetIncludes, cancellationToken);
            return assetData.From(m_DataSource, projectDescriptor, FieldsFilter.DefaultAssetIncludes);
        }

        /// <inheritdoc />
        public async Task<IDataset> GetDatasetAsync(DatasetDescriptor datasetDescriptor, CancellationToken cancellationToken)
        {
            var datasetData = await m_DataSource.GetDatasetAsync(datasetDescriptor, FieldsFilter.DefaultDatasetIncludes, cancellationToken);
            return datasetData.From(m_DataSource, datasetDescriptor.AssetDescriptor, FieldsFilter.DefaultDatasetIncludes.DatasetFields);
        }

        /// <inheritdoc />
        public async Task<ITransformation> GetTransformationAsync(TransformationDescriptor transformationDescriptor, CancellationToken cancellationToken)
        {
            var data = await m_DataSource.GetTransformationAsync(transformationDescriptor, cancellationToken);

            var transformation = new TransformationEntity(m_DataSource, transformationDescriptor);
            transformation.MapFrom(data);
            return transformation;
        }

        /// <inheritdoc />
        public async Task<IFile> GetFileAsync(FileDescriptor fileDescriptor, CancellationToken cancellationToken)
        {
            var fileData = await m_DataSource.GetFileAsync(fileDescriptor, FieldsFilter.DefaultFileIncludes, cancellationToken);
            return fileData.From(m_DataSource, fileDescriptor, FieldsFilter.DefaultFileIncludes.FileFields);
        }

        /// <inheritdoc />
        public FieldDefinitionQueryBuilder QueryFieldDefinitions(OrganizationId organizationId)
        {
            return new FieldDefinitionQueryBuilder(m_DataSource, organizationId);
        }

        /// <inheritdoc />
        public async Task<IFieldDefinition> GetFieldDefinitionAsync(FieldDefinitionDescriptor fieldDefinitionDescriptor, CancellationToken cancellationToken)
        {
            var data = await m_DataSource.GetFieldDefinitionAsync(fieldDefinitionDescriptor, cancellationToken);
            return data.From(m_DataSource, fieldDefinitionDescriptor);
        }

        /// <inheritdoc />
        public async Task<IFieldDefinition> CreateFieldDefinitionAsync(OrganizationId organizationId, IFieldDefinitionCreation fieldDefinitionCreation, CancellationToken cancellationToken)
        {
            var data = await m_DataSource.CreateFieldDefinitionAsync(organizationId, fieldDefinitionCreation.From(), cancellationToken);
            return data.From(m_DataSource, organizationId);
        }

        /// <inheritdoc />
        public Task DeleteFieldDefinitionAsync(FieldDefinitionDescriptor fieldDefinitionDescriptor, CancellationToken cancellationToken)
        {
            return m_DataSource.DeleteFieldDefinitionAsync(fieldDefinitionDescriptor, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<ILabel> CreateLabelAsync(OrganizationId organizationId, ILabelCreation labelCreation, CancellationToken cancellationToken)
        {
            var data = await m_DataSource.CreateLabelAsync(organizationId, labelCreation.From(), cancellationToken);
            return data.From(m_DataSource, organizationId);
        }

        /// <inheritdoc />
        public LabelQueryBuilder QueryLabels(OrganizationId organizationId)
        {
            return new LabelQueryBuilder(m_DataSource, organizationId);
        }

        /// <inheritdoc />
        public async Task<ILabel> GetLabelAsync(LabelDescriptor labelDescriptor, CancellationToken cancellationToken)
        {
            var data = await m_DataSource.GetLabelAsync(labelDescriptor, cancellationToken);
            return data.From(m_DataSource, labelDescriptor);
        }

        /// <inheritdoc />
        public StatusFlowQueryBuilder QueryStatusFlows(OrganizationId organizationId)
        {
            return new StatusFlowQueryBuilder(m_DataSource, organizationId);
        }

        /// <inheritdoc />
        public AssetDescriptor DeserializeAssetIdentifiers(string jsonSerialization)
        {
            // Verify old deprecated serialization format first
            var ids = IsolatedSerialization.DeserializeWithDefaultConverters<AssetIdentifier>(jsonSerialization);
            var projectId = ids.ProjectId.ToString();
            if (!string.IsNullOrEmpty(projectId) && projectId != ProjectId.None.ToString())
            {
                return ids.From();
            }

            return AssetDescriptor.FromJson(jsonSerialization);
        }

        /// <inheritdoc />
        public IAsset DeserializeAsset(string jsonSerialization)
        {
            if (jsonSerialization.Contains(AssetDataWithIdentifiers.SerializedType))
            {
                var data = IsolatedSerialization.DeserializeWithDefaultConverters<AssetDataWithIdentifiers>(jsonSerialization);
                return data.From(m_DataSource, FieldsFilter.All);
            }

            return null;
        }
    }
}
