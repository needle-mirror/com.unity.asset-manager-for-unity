using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cloud.CommonEmbedded;

namespace Unity.Cloud.AssetsEmbedded
{
    static partial class EntityMapper
    {
        internal static void MapFrom(this FileEntity file, IAssetDataSource assetDataSource, AssetDescriptor assetDescriptor, IFileData fileData, FileFields includeFields)
        {
            file.m_LinkedDatasets = fileData.DatasetIds?.Select(id => new DatasetDescriptor(assetDescriptor, id)).ToArray() ?? Array.Empty<DatasetDescriptor>();

            file.Tags = fileData.Tags ?? Array.Empty<string>();
            file.SystemTags = fileData.SystemTags ?? Array.Empty<string>();
            file.Status = fileData.Status;

            if (includeFields.HasFlag(FileFields.description))
                file.Description = fileData.Description ?? string.Empty;
            if (includeFields.HasFlag(FileFields.authoring))
                file.AuthoringInfo = new AuthoringInfo(fileData.CreatedBy, fileData.Created, fileData.UpdatedBy, fileData.Updated);

            if (includeFields.HasFlag(FileFields.downloadURL))
            {
                if (Uri.TryCreate(fileData.DownloadUrl, UriKind.RelativeOrAbsolute, out var downloadUrl))
                {
                    file.DownloadUrl = downloadUrl;
                    file.IsDownloadable = downloadUrl != null;
                }
                else
                {
                    file.DownloadUrl = null;
                    file.IsDownloadable = false;
                }
            }
            else
            {
                file.IsDownloadable = fileData.Status == "Uploaded";
            }

            if (includeFields.HasFlag(FileFields.previewURL))
            {
                Uri.TryCreate(fileData.PreviewUrl, UriKind.RelativeOrAbsolute, out var previewUrl);
                file.PreviewUrl = previewUrl;
            }

            if (includeFields.HasFlag(FileFields.metadata))
                file.MetadataEntity.Properties = fileData.Metadata?.From(assetDataSource, file.Descriptor.OrganizationId);
            if (includeFields.HasFlag(FileFields.systemMetadata))
                file.SystemMetadataEntity.Properties = fileData.SystemMetadata?.From();
            if (includeFields.HasFlag(FileFields.userChecksum))
                file.UserChecksum = fileData.UserChecksum;
            if (includeFields.HasFlag(FileFields.fileSize))
                file.SizeBytes = fileData.SizeBytes;
        }

        internal static FileEntity From(this IFileData fileData, IAssetDataSource assetDataSource, FileDescriptor fileDescriptor, FileFields includeFields)
        {
            var file = new FileEntity(assetDataSource, fileDescriptor);
            file.MapFrom(assetDataSource, fileDescriptor.DatasetDescriptor.AssetDescriptor, fileData, includeFields);
            return file;
        }

        internal static FileEntity From(this IFileData fileData, IAssetDataSource assetDataSource, AssetDescriptor assetDescriptor, FileFields includeFields)
        {
            // Because actions cannot be performed on files that is not linked to any dataset, we ignore these files.
            if (fileData.DatasetIds == null || !fileData.DatasetIds.Any()) return null;

            var fileDescriptor = new FileDescriptor(new DatasetDescriptor(assetDescriptor, fileData.DatasetIds.First()), fileData.Path);
            return fileData.From(assetDataSource, fileDescriptor, includeFields);
        }

        internal static IFileBaseData From(this IFileUpdate fileUpdate)
        {
            return new FileBaseData
            {
                Description = fileUpdate.Description,
                Tags = fileUpdate.Tags,
            };
        }
    }
}
