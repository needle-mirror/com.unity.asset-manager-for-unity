using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Shared factory for creating editable <see cref="MetadataElement"/> widgets from a field definition
    /// and a list of metadata values.
    /// </summary>
    static class MetadataFieldFactory
    {
        /// <summary>
        /// Creates the type-specific <see cref="MetadataElement"/> editor for the given field definition.
        /// </summary>
        /// <param name="fieldDefinition">The field definition describing the metadata type and (for selection types) accepted values.</param>
        /// <param name="metadataList">One metadata instance per asset being edited.</param>
        /// <param name="userInfos">User look-up list, required only for <see cref="MetadataFieldType.User"/> fields. May be null for other types.</param>
        /// <returns>A <see cref="MetadataElement"/> ready to be added to the visual tree.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="fieldDefinition"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown for unrecognised <see cref="MetadataFieldType"/> values.</exception>
        public static MetadataElement CreateEditField(
            IMetadataFieldDefinition fieldDefinition,
            List<IMetadata> metadataList,
            List<UserInfo> userInfos = null)
        {
            if (fieldDefinition == null)
                throw new ArgumentNullException(nameof(fieldDefinition));

            return fieldDefinition.Type switch
            {
                MetadataFieldType.Text => new TextMetadataField(metadataList.Cast<TextMetadata>().ToList()),
                MetadataFieldType.Number => new NumberMetadataField(metadataList.Cast<NumberMetadata>().ToList()),
                MetadataFieldType.Boolean => new BooleanMetadataField(metadataList.Cast<BooleanMetadata>().ToList()),
                MetadataFieldType.Url => new UrlMetadataField(metadataList.Cast<UrlMetadata>().ToList()),
                MetadataFieldType.Timestamp => new TimestampMetadataField(metadataList.Cast<TimestampMetadata>().ToList()),
                MetadataFieldType.User => new UserMetadataField(
                    metadataList.Cast<UserMetadata>().ToList(),
                    userInfos?.ToList() ?? new List<UserInfo>()),
                MetadataFieldType.SingleSelection => new SingleSelectionMetadataField(
                    metadataList.Cast<SingleSelectionMetadata>().ToList(),
                    (fieldDefinition as SelectionFieldDefinition)?.AcceptedValues?.ToList() ?? new List<string>()),
                MetadataFieldType.MultiSelection => new MultiSelectionMetadataField(
                    fieldDefinition.DisplayName,
                    metadataList.Cast<MultiSelectionMetadata>().ToList(),
                    (fieldDefinition as SelectionFieldDefinition)?.AcceptedValues?.ToList() ?? new List<string>()),
                _ => throw new InvalidOperationException(Constants.UnexpectedFieldDefinitionType)
            };
        }
    }
}

