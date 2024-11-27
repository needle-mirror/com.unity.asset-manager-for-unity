using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    static partial class EntityMapper
    {
        internal static void MapFrom(this AssetEntity asset, IAssetDataSource assetDataSource, AssetDescriptor assetDescriptor, IAssetData assetData, FieldsFilter includeFields)
        {
            includeFields ??= new FieldsFilter();

            var organizationId = assetDescriptor.OrganizationId;

            asset.m_LinkedProjects = assetData.LinkedProjectIds?.Select(projectId => new ProjectDescriptor(organizationId, projectId)).ToArray() ?? Array.Empty<ProjectDescriptor>();
            asset.SourceProject = new ProjectDescriptor(organizationId, assetData.SourceProjectId);

            asset.Name = assetData.Name;
            asset.Tags = assetData.Tags ?? Array.Empty<string>();
            asset.SystemTags = assetData.SystemTags ?? Array.Empty<string>();
            asset.Type = assetData.Type ?? AssetType.Other;
            asset.Status = assetData.Status;
            asset.StatusName = assetData.Status;

            if (includeFields.AssetFields.HasFlag(AssetFields.description))
            {
                asset.Description = assetData.Description ?? string.Empty;
            }

            if (includeFields.AssetFields.HasFlag(AssetFields.versioning))
            {
                if (assetData.IsFrozen)
                {
                    asset.State = AssetState.Frozen;
                }
                else if (assetData.AutoSubmit)
                {
                    asset.State = AssetState.PendingFreeze;
                }
                else
                {
                    asset.State = AssetState.Unfrozen;
                }
                asset.FrozenSequenceNumber = assetData.VersionNumber;
                asset.Changelog = assetData.Changelog;
                asset.ParentVersion = assetData.ParentVersion;
                asset.ParentFrozenSequenceNumber = assetData.ParentVersionNumber;
            }

            if (includeFields.AssetFields.HasFlag(AssetFields.labels))
            {
                asset.Labels = assetData.Labels?.Select(x => new LabelDescriptor(organizationId, x)) ?? Array.Empty<LabelDescriptor>();
                asset.ArchivedLabels = assetData.ArchivedLabels?.Select(x => new LabelDescriptor(organizationId, x)) ?? Array.Empty<LabelDescriptor>();
            }

            if (includeFields.AssetFields.HasFlag(AssetFields.previewFile))
            {
                asset.StatusFlowDescriptor = new StatusFlowDescriptor(organizationId, assetData.StatusFlowId);
                asset.PreviewFile = assetData.PreviewFilePath ?? string.Empty;
                var previewFileDatasetDescriptor = new DatasetDescriptor(assetDescriptor, assetData.PreviewFileDatasetId);
                asset.PreviewFileDescriptor = new FileDescriptor(previewFileDatasetDescriptor, assetData.PreviewFilePath ?? string.Empty);
            }

            if (includeFields.AssetFields.HasFlag(AssetFields.authoring))
                asset.AuthoringInfo = new AuthoringInfo(assetData.CreatedBy, assetData.Created, assetData.UpdatedBy, assetData.Updated);

            if (includeFields.AssetFields.HasFlag(AssetFields.metadata))
                asset.MetadataEntity.Properties = assetData.Metadata?.From(assetDataSource, asset.Descriptor.OrganizationId);

            if (includeFields.AssetFields.HasFlag(AssetFields.systemMetadata))
                asset.SystemMetadataEntity.Properties = assetData.SystemMetadata?.From();

            if (includeFields.AssetFields.HasFlag(AssetFields.previewFileUrl))
            {
                Uri.TryCreate(assetData.PreviewFileUrl, UriKind.RelativeOrAbsolute, out var previewFileDownloadUrl);
                asset.PreviewFileUrl = previewFileDownloadUrl;
            }
        }

        internal static AssetCreateData From(this IAssetCreation assetCreation)
        {
            return new AssetCreateData
            {
                Name = assetCreation.Name,
                Description = assetCreation.Description,
                Tags = assetCreation.Tags?.Where(s => !string.IsNullOrWhiteSpace(s)),
                StatusFlowId = assetCreation.StatusFlowDescriptor?.StatusFlowId,
                Type = assetCreation.Type,
                Metadata = assetCreation.Metadata?.ToObjectDictionary() ?? new Dictionary<string, object>(),
                Collections = assetCreation.Collections,
            };
        }

        internal static bool HasValues(this IAssetUpdate assetUpdate)
        {
            return assetUpdate.Name != null ||
                assetUpdate.Description != null ||
                assetUpdate.Tags != null ||
                assetUpdate.Type.HasValue ||
                assetUpdate.PreviewFile != null;
        }

        internal static IAssetUpdateData From(this IAssetUpdate assetUpdate)
        {
            return new AssetUpdateData
            {
                Name = assetUpdate.Name,
                Description = assetUpdate.Description,
                Tags = assetUpdate.Tags?.Where(s => !string.IsNullOrWhiteSpace(s)),
                Type = assetUpdate.Type,
                PreviewFile = assetUpdate.PreviewFile,
            };
        }

        internal static AssetEntity From(this IAssetData data, IAssetDataSource assetDataSource, OrganizationId organizationId, IEnumerable<ProjectId> availableProjects, FieldsFilter includeFields)
        {
            var validProjects = new HashSet<ProjectId>(availableProjects);
            validProjects.IntersectWith(data.LinkedProjectIds ?? Array.Empty<ProjectId>());

            var projectId = data.SourceProjectId;
            if (validProjects.Any() && !validProjects.Contains(projectId))
            {
                projectId = validProjects.First();
            }

            return data.From(assetDataSource, new ProjectDescriptor(organizationId, projectId), includeFields);
        }

        internal static AssetEntity From(this IAssetData data, IAssetDataSource assetDataSource, ProjectDescriptor projectDescriptor, FieldsFilter includeFields)
        {
            var descriptor = new AssetDescriptor(projectDescriptor, data.Id, data.Version);
            return data.From(assetDataSource, descriptor, includeFields);
        }

        internal static AssetEntity From(this IAssetData data, IAssetDataSource assetDataSource, AssetDescriptor assetDescriptor, FieldsFilter includeFields)
        {
            var asset = new AssetEntity(assetDataSource, assetDescriptor);
            asset.MapFrom(assetDataSource, assetDescriptor, data, includeFields);
            return asset;
        }

        internal static IAsset From(this AssetDataWithIdentifiers data, IAssetDataSource dataSource, FieldsFilter includeFields)
        {
#pragma warning disable 618
            var assetDescriptor = string.IsNullOrEmpty(data.Descriptor) ? data.Identifier.From() : AssetDescriptor.FromJson(data.Descriptor);
            return data.Data.From(dataSource, assetDescriptor, includeFields);
#pragma warning restore 618
        }

        internal static AssetDescriptor From(this AssetIdentifier ids)
        {
            var projectDescriptor = new ProjectDescriptor(ids.OrganizationId, ids.ProjectId);
            return new AssetDescriptor(projectDescriptor, ids.Id, ids.Version);
        }

        internal static AssetData From(this AssetEntity asset)
        {
            return new AssetData(asset.Descriptor.AssetId, asset.Descriptor.AssetVersion)
            {
                Name = asset.Name,
                Description = asset.Description,
                Tags = asset.Tags?.ToList(),
                Type = asset.Type,
                PreviewFilePath = asset.PreviewFileDescriptor.Path,
                PreviewFileDatasetId = asset.PreviewFileDescriptor.DatasetId,
                PreviewFileUrl = asset.PreviewFileUrl?.ToString(),
                Status = asset.StatusName,
                Created = asset.AuthoringInfo?.Created,
                CreatedBy = asset.AuthoringInfo?.CreatedBy.ToString(),
                Updated = asset.AuthoringInfo?.Updated,
                UpdatedBy = asset.AuthoringInfo?.UpdatedBy.ToString(),
                SourceProjectId = asset.SourceProject.ProjectId,
                LinkedProjectIds = asset.LinkedProjects.Select(project => project.ProjectId).ToList(),
                Metadata = asset.MetadataEntity.From(),
                SystemTags = asset.SystemTags,
                Labels = asset.Labels?.Select(x => x.LabelName),
                ArchivedLabels = asset.ArchivedLabels?.Select(x => x.LabelName),
                ParentVersion = asset.ParentVersion,
                ParentVersionNumber = asset.ParentFrozenSequenceNumber,
                IsFrozen = asset.State == AssetState.Frozen,
                AutoSubmit = asset.State == AssetState.PendingFreeze,
                VersionNumber = asset.FrozenSequenceNumber,
                Changelog = asset.Changelog,
            };
        }
    }
}
