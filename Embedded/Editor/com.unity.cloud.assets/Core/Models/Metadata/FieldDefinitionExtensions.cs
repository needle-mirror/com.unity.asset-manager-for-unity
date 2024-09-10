using System;

namespace Unity.Cloud.AssetsEmbedded
{
    static class FieldDefinitionExtensions
    {
        /// <summary>
        /// Returns the field definition as a <see cref="ISelectionFieldDefinition"/>.
        /// </summary>
        /// <param name="fieldDefinition">A field definition. </param>
        /// <returns>A <see cref="ISelectionFieldDefinition"/>. </returns>
        /// <exception cref="InvalidCastException">If the field definition is not of type <see cref="FieldDefinitionType.Selection"/></exception>
        public static ISelectionFieldDefinition AsSelectionFieldDefinition(this IFieldDefinition fieldDefinition)
        {
            if (fieldDefinition is ISelectionFieldDefinition selectionFieldDefinition)
            {
                return selectionFieldDefinition;
            }

            throw new InvalidCastException("Field definition is not a selection field definition.");
        }
    }
}
