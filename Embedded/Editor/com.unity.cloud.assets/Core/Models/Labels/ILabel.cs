using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Cloud.AssetsEmbedded
{
    interface ILabel
    {
        /// <summary>
        /// The descriptor for the label.
        /// </summary>
        LabelDescriptor Descriptor { get; }

        /// <inheritdoc cref="LabelDescriptor.LabelName"/>
        string Name => Descriptor.LabelName;

        /// <summary>
        /// The description of the label.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Whether the label is a system label.
        /// </summary>
        bool IsSystemLabel { get; }

        /// <summary>
        /// Whether the label can be manually assigned to an asset.
        /// </summary>
        bool IsAssignable { get; }

        /// <summary>
        /// The authoring information for the label.
        /// </summary>
        AuthoringInfo AuthoringInfo { get; }

        /// <summary>
        /// The color of the label.
        /// </summary>
        Color DisplayColor { get; }

        /// <summary>
        /// Fetches the latest changes.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        Task RefreshAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Updates the label.
        /// </summary>
        /// <param name="labelUpdate">The object containing information to update the label. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        Task UpdateAsync(ILabelUpdate labelUpdate, CancellationToken cancellationToken);

        /// <summary>
        /// Updates the label name.
        /// </summary>
        /// <param name="labelName">A new unique name for the label. </param>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        Task RenameAsync(string labelName, CancellationToken cancellationToken);

        /// <summary>
        /// Archives the label.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        Task ArchiveAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Unarchives the label.
        /// </summary>
        /// <param name="cancellationToken">A token that can be used to cancel the request. </param>
        /// <returns>A task with no result. </returns>
        Task UnarchiveAsync(CancellationToken cancellationToken);
    }
}
