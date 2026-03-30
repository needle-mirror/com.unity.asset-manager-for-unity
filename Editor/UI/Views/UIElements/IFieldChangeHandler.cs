using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Abstracts the save mechanism for field edits so the same UI entry classes can be used
    /// with different backends (e.g. local staging for upload, async server saves for inline editing).
    /// </summary>
    interface IFieldChangeHandler
    {
        /// <summary>
        /// Persists the given edits. Implementations may save synchronously (local staging)
        /// or asynchronously (server round-trip).
        /// </summary>
        Task HandleChangesAsync(IEnumerable<AssetFieldEdit> edits);
    }

    /// <summary>
    /// Saves edits by delegating to <see cref="IInlineEditService.SaveFieldAsync"/> for each edit
    /// (via <see cref="MultiEditHelpers.SaveToAllAsync"/>). Used by the Assets tab.
    /// </summary>
    class InlineEditServiceFieldChangeHandler : IFieldChangeHandler
    {
        readonly IInlineEditService m_Service;

        public InlineEditServiceFieldChangeHandler(IInlineEditService service)
        {
            m_Service = Guard.AgainstNull(service, nameof(service));
        }

        public async Task HandleChangesAsync(IEnumerable<AssetFieldEdit> edits)
        {
            var editList = edits.ToList();
            if (editList.Count == 0)
                return;

            var first = editList[0];
            var identifiers = editList.Select(e => e.AssetIdentifier).ToList();

            var result = await MultiEditHelpers.SaveToAllAsync(m_Service, identifiers, first.Field, first.EditValue);

            if (!result.AllSucceeded)
                Utilities.DevLogError($"Field change handler save failed: {result.GetSummaryMessage()}");
        }
    }

    /// <summary>
    /// Saves edits by invoking a synchronous callback (e.g. staging changes locally in
    /// upload asset data). Used by the Upload tab. Returns <see cref="Task.CompletedTask"/>.
    /// </summary>
    class LocalStagingFieldChangeHandler : IFieldChangeHandler
    {
        readonly Action<IEnumerable<AssetFieldEdit>> m_Callback;

        public LocalStagingFieldChangeHandler(Action<IEnumerable<AssetFieldEdit>> callback)
        {
            m_Callback = Guard.AgainstNull(callback, nameof(callback));
        }

        public Task HandleChangesAsync(IEnumerable<AssetFieldEdit> edits)
        {
            m_Callback(edits);
            return Task.CompletedTask;
        }
    }
}


