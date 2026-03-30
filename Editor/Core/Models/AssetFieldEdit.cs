using System;
using System.Collections.Generic;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Enum representing the different fields that can be edited on an asset
    /// </summary>
    enum EditField
    {
        Name,
        Description,
        Status,
        Tags,
        Custom,
    }

    /// <summary>
    /// Represents an edit operation on a specific field of an asset
    /// </summary>
    class AssetFieldEdit
    {
        static readonly Dictionary<EditField, Type> k_FieldTypeMap = new()
        {
            { EditField.Name, typeof(string) },
            { EditField.Description, typeof(string) },
            { EditField.Status, typeof(string) },
            { EditField.Tags, typeof(IEnumerable<string>) },
            { EditField.Custom, typeof(IMetadata) },
        };

        public AssetIdentifier AssetIdentifier { get; }
        public EditField Field { get; }
        public object EditValue { get; }
        Type EditValueType => k_FieldTypeMap[Field];

        public AssetFieldEdit(AssetIdentifier assetIdentifier, EditField field, object editValue)
        {
            AssetIdentifier = assetIdentifier;
            Field = field;
            EditValue = editValue;

            ValidateFieldType();
        }

        void ValidateFieldType()
        {
            if (EditValue == null || !EditValueType.IsInstanceOfType(EditValue))
                throw new ArgumentException($"AssetFieldEdit with type \"{Field.ToString()}\" must be assignable to \"{EditValueType.Name}\"");
        }
    }
}
