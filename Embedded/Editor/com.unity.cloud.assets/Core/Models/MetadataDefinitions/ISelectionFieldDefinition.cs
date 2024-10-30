using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Cloud.AssetsEmbedded
{
    interface ISelectionFieldDefinition : IFieldDefinition
    {
        /// <summary>
        /// The accepted values of the field.
        /// <remarks>This is only required for field definitions of type <see cref="FieldDefinitionType.Selection"/>.</remarks>
        /// </summary>
        IEnumerable<string> AcceptedValues { get; }

        /// <summary>
        /// Whether the field can have multiple values.
        /// <remarks>This is only requred for field definitions of type <see cref="FieldDefinitionType.Selection"/>.</remarks>
        /// </summary>
        bool Multiselection { get; }

        /// <summary>
        /// Sets the parameter list as the accepted values of the field.
        /// </summary>
        /// <param name="acceptedValues">An enumeration of accepted values. </param>
        /// <param name="cancellationToken">The cancellation token. </param>
        /// <returns>A task with no result. </returns>
        Task SetSelectionValuesAsync(IEnumerable<string> acceptedValues, CancellationToken cancellationToken);

        /// <summary>
        /// Appends the parameter list to the accepted values of the field.
        /// </summary>
        /// <param name="acceptedValues">An enumeration of accepted values. </param>
        /// <param name="cancellationToken">The cancellation token. </param>
        /// <returns>A task with no result. </returns>
        Task AddSelectionValuesAsync(IEnumerable<string> acceptedValues, CancellationToken cancellationToken);

        /// <summary>
        /// Removes the parameter list from the accepted values of the field.
        /// </summary>
        /// <param name="acceptedValues">An enumeration of accepted values. </param>
        /// <param name="cancellationToken">The cancellation token. </param>
        /// <returns>A task with no result. </returns>
        Task RemoveSelectionValuesAsync(IEnumerable<string> acceptedValues, CancellationToken cancellationToken);
    }
}
