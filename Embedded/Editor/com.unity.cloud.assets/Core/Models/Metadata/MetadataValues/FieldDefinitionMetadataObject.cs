using System;
using System.Collections;
using System.Threading.Tasks;

namespace Unity.Cloud.AssetsEmbedded
{
    class FieldDefinitionMetadataObject : MetadataObject
    {
        internal FieldDefinitionMetadataObject(object value)
            : base(ParseType(value), value) { }

        internal FieldDefinitionMetadataObject(object value, IAssetDataSource dataSource, FieldDefinitionDescriptor fieldDefinitionDescriptor)
            : base(ParseType(value), value)
        {
            _ = ValidateAsync(dataSource, fieldDefinitionDescriptor);
        }

        static MetadataValueType ParseType(object value)
        {
            return value switch
            {
                bool => MetadataValueType.Boolean,
                ICollection => MetadataValueType.MultiSelection,
                double or int or float or long or short or byte or sbyte or decimal => MetadataValueType.Number,
                DateTime => MetadataValueType.Timestamp,
                string stringValue => UrlMetadata.TryParse(stringValue, out _, out _) ? MetadataValueType.Url : MetadataValueType.Unknown,
                _ => MetadataValueType.Unknown
            };
        }

        async Task ValidateAsync(IAssetDataSource dataSource, FieldDefinitionDescriptor fieldDefinitionDescriptor)
        {
            if (ValueType == MetadataValueType.Unknown)
            {
                try
                {
                    var fieldDefinition = await dataSource.GetFieldDefinitionAsync(fieldDefinitionDescriptor, default);
                    var multiSelection = fieldDefinition.Multiselection ?? false;
                    ValueType = fieldDefinition.Type switch
                    {
                        FieldDefinitionType.Boolean => MetadataValueType.Boolean,
                        FieldDefinitionType.Number => MetadataValueType.Number,
                        FieldDefinitionType.Text => MetadataValueType.Text,
                        FieldDefinitionType.Timestamp => MetadataValueType.Timestamp,
                        FieldDefinitionType.Url => MetadataValueType.Url,
                        FieldDefinitionType.User => MetadataValueType.User,
                        FieldDefinitionType.Selection => multiSelection ? MetadataValueType.MultiSelection : MetadataValueType.SingleSelection,
                        _ => MetadataValueType.Unknown
                    };
                }
                catch (Exception)
                {
                    // ignored - we'll just leave it as unknown
                }
            }
        }
    }
}
