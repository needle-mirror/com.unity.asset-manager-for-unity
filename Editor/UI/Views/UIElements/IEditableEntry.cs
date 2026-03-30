using System;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Controls how metadata fields are presented and saved in the inspector.
    /// </summary>
    enum EditingMode
    {
        ReadOnly, // Viewer, or Assets page before permission check resolves
        Upload,   // Upload page: always-visible fields, batched saves
        Inline    // Assets page + Contributor: click-to-edit, per-field save
    }

    /// <summary>
    /// Interface for editable inspector entries that support different editing modes.
    /// Implements IDisposable to ensure proper cleanup of resources like confirmation popups.
    /// </summary>
    /// <remarks>
    /// Implementers must dispose of any <see cref="InlineEditConfirmationPopupContainer"/>
    /// instances and unregister event handlers in their Dispose implementation.
    /// Disposal should be idempotent - calling Dispose multiple times should be safe.
    /// </remarks>
    interface IEditableEntry : IDisposable
    {
        /// <summary>
        /// The unique identifier of the asset this entry edits.
        /// </summary>
        string AssetId { get; }

        /// <summary>
        /// Whether editing is currently enabled for this entry.
        /// </summary>
        bool IsEditingEnabled { get; }

        /// <summary>
        /// Whether this entry supports editing multiple selected assets simultaneously.
        /// </summary>
        bool AllowMultiSelection { get; }

        /// <summary>
        /// Raised when the entry value has been edited by the user.
        /// </summary>
        event Action<object> EntryEdited;

        /// <summary>
        /// Called to check if the current value differs from the original value.
        /// Parameters: (assetId, currentValue) -> bool indicating if edited.
        /// </summary>
        event Func<string, object, bool> IsEntryEdited;

        /// <summary>
        /// Configures the entry for the specified editing mode.
        /// </summary>
        void ConfigureEditing(EditingMode mode);

        /// <summary>
        /// Saves any pending edits that haven't been committed yet.
        /// </summary>
        void SavePendingEdits();
    }
}
