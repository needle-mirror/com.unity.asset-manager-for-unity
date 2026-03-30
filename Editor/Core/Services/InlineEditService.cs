using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Result of an inline edit operation
    /// </summary>
    class InlineEditResult
    {
        public bool Success { get; }
        public string ErrorMessage { get; }

        public InlineEditResult(bool success, string errorMessage = null)
        {
            Success = success;
            ErrorMessage = errorMessage;
        }

        public static InlineEditResult SuccessResult() => new InlineEditResult(true);
        public static InlineEditResult FailureResult(string errorMessage) => new InlineEditResult(false, errorMessage);
    }

    /// <summary>
    /// Service for handling inline editing of asset metadata
    /// </summary>
    interface IInlineEditService : IService
    {
        /// <summary>
        /// Saves a field edit to the cloud
        /// </summary>
        /// <param name="edit">The field edit to save</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Result indicating success or failure with error message</returns>
        Task<InlineEditResult> SaveFieldAsync(AssetFieldEdit edit, CancellationToken token);

        /// <summary>
        /// Checks if the current user can edit assets (synchronous, uses cached permissions)
        /// </summary>
        /// <param name="assetId">Asset identifier</param>
        /// <returns>True if user has Contributor role or higher</returns>
        bool CanEdit(AssetIdentifier assetId);

        /// <summary>
        /// Event fired when a field is successfully saved
        /// </summary>
        event Action<AssetFieldEdit> FieldSaved;

        /// <summary>
        /// Adds a new custom metadata field to an asset with default value from the field definition.
        /// </summary>
        Task<InlineEditResult> AddFieldAsync(AssetIdentifier identifier, IMetadataFieldDefinition fieldDef, CancellationToken token);

        /// <summary>
        /// Removes a custom metadata field from an asset.
        /// </summary>
        Task<InlineEditResult> RemoveFieldAsync(AssetIdentifier identifier, string fieldKey, CancellationToken token);

        /// <summary>
        /// Saves the same field edit to multiple assets in parallel. Attempts all saves and returns the first failure, if any.
        /// </summary>
        Task<InlineEditResult> SaveFieldToMultipleAssetsAsync(IEnumerable<AssetFieldEdit> edits, CancellationToken token);

        /// <summary>
        /// Returns true if any save operation is currently in progress.
        /// </summary>
        bool HasAnySaveInProgress { get; }

        /// <summary>
        /// Fired when the saving state transitions (idle to saving, or saving to idle).
        /// </summary>
        event Action SavingStateChanged;
    }

    [Serializable]
    class InlineEditService : BaseService<IInlineEditService>, IInlineEditService
    {
        [SerializeReference]
        IAssetsProvider m_AssetsProvider;

        [SerializeReference]
        IAssetDataManager m_AssetDataManager;

        [SerializeReference]
        IPermissionsManager m_PermissionsManager;

        [SerializeReference]
        IMessageManager m_MessageManager;

        public event Action<AssetFieldEdit> FieldSaved;
        public event Action SavingStateChanged;

        int m_ActiveSaveCount;
        readonly object m_SaveCountLock = new object();

        public bool HasAnySaveInProgress
        {
            get { lock (m_SaveCountLock) return m_ActiveSaveCount > 0; }
        }

        void IncrementSaveCount()
        {
            bool wasZero;
            lock (m_SaveCountLock)
            {
                wasZero = m_ActiveSaveCount == 0;
                m_ActiveSaveCount++;
            }

            if (wasZero)
                SavingStateChanged?.Invoke();
        }

        void DecrementSaveCount()
        {
            bool isNowZero;
            lock (m_SaveCountLock)
            {
                m_ActiveSaveCount = Math.Max(0, m_ActiveSaveCount - 1);
                isNowZero = m_ActiveSaveCount == 0;
            }

            if (isNowZero)
                SavingStateChanged?.Invoke();
        }

        [ServiceInjection]
        public void Inject(IAssetsProvider assetsProvider, IAssetDataManager assetDataManager,
            IPermissionsManager permissionsManager, IMessageManager messageManager)
        {
            m_AssetsProvider = assetsProvider;
            m_AssetDataManager = assetDataManager;
            m_PermissionsManager = permissionsManager;
            m_MessageManager = messageManager;
        }

        protected override void ValidateServiceDependencies()
        {
            base.ValidateServiceDependencies();
            m_AssetsProvider ??= ServicesContainer.instance.Get<IAssetsProvider>();
            m_AssetDataManager ??= ServicesContainer.instance.Get<IAssetDataManager>();
            m_PermissionsManager ??= ServicesContainer.instance.Get<IPermissionsManager>();
            m_MessageManager ??= ServicesContainer.instance.Get<IMessageManager>();
        }

        public async Task<InlineEditResult> SaveFieldAsync(AssetFieldEdit edit, CancellationToken token)
        {
            if (edit == null)
                return InlineEditResult.FailureResult("Edit cannot be null");

            IncrementSaveCount();
            try
            {
                return await ExecuteEditAsync(edit.AssetIdentifier, edit.Field, async assetData =>
                {
                    switch (edit.Field)
                    {
                        case EditField.Name:
                        case EditField.Description:
                        case EditField.Tags:
                            await SavePrimaryFieldAsync(assetData, edit, token);
                            break;
                        case EditField.Status:
                            await SaveStatusFieldAsync(assetData, edit, token);
                            break;
                        case EditField.Custom:
                            await SaveCustomMetadataFieldAsync(assetData, edit, token);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(edit.Field), $"Unknown field type: {edit.Field}");
                    }

                    Utilities.DevLog($"Inline edit saved: {edit.Field} for asset {assetData.Name}");
                    FieldSaved?.Invoke(edit);
                });
            }
            finally
            {
                DecrementSaveCount();
            }
        }

        async Task SavePrimaryFieldAsync(AssetData assetData, AssetFieldEdit edit, CancellationToken token)
        {
            var update = new AssetUpdate();

            switch (edit.Field)
            {
                case EditField.Name:
                    update.Name = edit.EditValue as string;
                    break;

                case EditField.Description:
                    update.Description = edit.EditValue as string;
                    break;

                case EditField.Tags:
                    if (edit.EditValue is IEnumerable<string> tags)
                    {
                        update.Tags = tags.ToList();
                    }
                    break;
            }

            await m_AssetsProvider.UpdateAsync(assetData, update, token);
        }

        async Task SaveStatusFieldAsync(AssetData assetData, AssetFieldEdit edit, CancellationToken token)
        {
            var statusName = (edit.EditValue as string)?.Trim();
            if (string.IsNullOrEmpty(statusName))
                throw new ArgumentException("Status name cannot be null or empty");

            var currentStatus = (assetData.Status ?? string.Empty).Trim();

            // Normalize both sides: strip whitespace and compare case-insensitively so that
            // API format ("InReview") and UI format ("In Review") are treated as equivalent.
            var normalizedCurrent = currentStatus.Replace(" ", "");
            var normalizedNew = statusName.Replace(" ", "");
            if (string.Equals(normalizedCurrent, normalizedNew, StringComparison.OrdinalIgnoreCase))
            {
                await assetData.RefreshPropertiesAsync(token);
                return;
            }

            var orgProvider = ServicesContainer.instance != null
                ? ServicesContainer.instance.Resolve<IProjectOrganizationProvider>()
                : null;

            var organization = orgProvider?.SelectedOrganization;
            if (organization != null)
            {
                var statusFlowInfo = await organization.GetStatusFlowInfoAsync(assetData, token);
                if (statusFlowInfo != null)
                {
                    var path = statusFlowInfo.GetTransitionPath(currentStatus, statusName);
                    if (path != null && path.Length == 1)
                    {
                        await assetData.RefreshPropertiesAsync(token);
                        return;
                    }

                    if (path != null && path.Length > 1)
                    {
                        for (var i = 1; i < path.Length; i++)
                            await m_AssetsProvider.UpdateStatusAsync(assetData, path[i], token);

                        await assetData.RefreshPropertiesAsync(token);

                        // Reachable statuses depend on the current status, update immediately.
                        assetData.ReachableStatusNames = await m_AssetsProvider.GetReachableStatusNamesAsync(assetData.Identifier, token);
                        return;
                    }
                }
            }

            // Fallback: no status flow info available, attempt direct update.
            // AssetsSdkProvider.UpdateStatusAsync has its own X→X guard.
            await m_AssetsProvider.UpdateStatusAsync(assetData, statusName, token);
            await assetData.RefreshPropertiesAsync(token);

            // Reachable statuses depend on the current status — fetch directly to bypass the CachedTask keepAlive window
            assetData.ReachableStatusNames = await m_AssetsProvider.GetReachableStatusNamesAsync(assetData.Identifier, token);
        }

        async Task SaveCustomMetadataFieldAsync(AssetData assetData, AssetFieldEdit edit, CancellationToken token)
        {
            var metadata = edit.EditValue as IMetadata;
            if (metadata == null)
                throw new ArgumentException("Custom metadata edit requires IMetadata value");

            // Preserve field order by replacing in-place, or append if new
            var currentList = assetData.Metadata.ToList();
            var existingIndex = currentList.FindIndex(m => m.FieldKey == metadata.FieldKey);
            if (existingIndex >= 0)
                currentList[existingIndex] = metadata;
            else
                currentList.Add(metadata);

            var update = new AssetUpdate { Metadata = currentList };
            await m_AssetsProvider.UpdateAsync(assetData, update, token);

            // Update local data immediately so UI refresh shows the new value
            assetData.SetMetadata(currentList);
        }

        public async Task<InlineEditResult> RemoveFieldAsync(AssetIdentifier identifier, string fieldKey, CancellationToken token)
        {
            if (identifier == null || string.IsNullOrEmpty(fieldKey))
                return InlineEditResult.FailureResult("Invalid identifier or field key");

            IncrementSaveCount();
            try
            {
                return await ExecuteEditAsync(identifier, EditField.Custom, async assetData =>
                {
                    var updatedList = assetData.Metadata.Where(m => m.FieldKey != fieldKey).ToList();
                    await m_AssetsProvider.UpdateAsync(assetData, new AssetUpdate { Metadata = updatedList }, token);
                    assetData.SetMetadata(updatedList);
                });
            }
            finally
            {
                DecrementSaveCount();
            }
        }

        public async Task<InlineEditResult> AddFieldAsync(AssetIdentifier identifier, IMetadataFieldDefinition fieldDef, CancellationToken token)
        {
            if (identifier == null || fieldDef == null)
                return InlineEditResult.FailureResult("Invalid identifier or field definition");

            IncrementSaveCount();
            try
            {
                return await ExecuteEditAsync(identifier, EditField.Custom, async assetData =>
                {
                    var newMetadata = MetadataHelpers.CreateMetadataFromFieldDefinition(fieldDef);
                    var updatedList = assetData.Metadata.Where(m => m.FieldKey != newMetadata.FieldKey).ToList();
                    updatedList.Add(newMetadata);
                    await m_AssetsProvider.UpdateAsync(assetData, new AssetUpdate { Metadata = updatedList }, token);
                    assetData.SetMetadata(updatedList);
                    FieldSaved?.Invoke(new AssetFieldEdit(identifier, EditField.Custom, newMetadata));
                });
            }
            finally
            {
                DecrementSaveCount();
            }
        }

        public async Task<InlineEditResult> SaveFieldToMultipleAssetsAsync(IEnumerable<AssetFieldEdit> edits, CancellationToken token)
        {
            if (edits == null)
                return InlineEditResult.FailureResult("Edits cannot be null");

            if (token.IsCancellationRequested)
                return InlineEditResult.FailureResult("Operation was cancelled");

            var editList = edits.ToList();
            if (editList.Count == 0)
                return InlineEditResult.SuccessResult();

            IncrementSaveCount();
            try
            {
                var taskCreations = editList
                    .Select<AssetFieldEdit, Func<Task<InlineEditResult>>>(edit => () => SaveFieldAsync(edit, token));

                var tasks = await TaskUtils.RunAllTasksInQueue(taskCreations, cancellationToken: token);

                foreach (var task in tasks)
                {
                    var result = await task;
                    if (!result.Success)
                        return result;
                }

                return InlineEditResult.SuccessResult();
            }
            finally
            {
                DecrementSaveCount();
            }
        }

        async Task<InlineEditResult> ExecuteEditAsync(AssetIdentifier identifier, EditField field, Func<AssetData, Task> operation)
        {
            if (!CanEdit(identifier))
                return InlineEditResult.FailureResult("You do not have permission to edit this asset");

            var baseAssetData = m_AssetDataManager.GetAssetData(identifier);
            if (baseAssetData is not AssetData assetData)
                return InlineEditResult.FailureResult("Asset not found");

            try
            {
                await operation(assetData);
                return InlineEditResult.SuccessResult();
            }
            catch (OperationCanceledException)
            {
                return InlineEditResult.FailureResult("Operation was cancelled");
            }
            catch (Exception ex)
            {
                return HandleEditError(ex, field);
            }
        }

        InlineEditResult HandleEditError(Exception ex, EditField field)
        {
            var errorMessage = GetUserFriendlyErrorMessage(ex, field);
            Utilities.DevLogError($"Failed to save {field}: {Utilities.GetUserFacingErrorMessage(ex)}");
            m_MessageManager?.SetHelpBoxMessage(new HelpBoxMessage(
                errorMessage, RecommendedAction.None, HelpBoxMessageType.Error, dismissable: true));
            return InlineEditResult.FailureResult(errorMessage);
        }

        public bool CanEdit(AssetIdentifier assetId)
        {
            if (assetId == null)
                return false;

            // Check if user has Contributor role (cached check, no async needed)
            // GetRoleAsync will return cached value if available, otherwise return None
            var roleTask = m_PermissionsManager.GetRoleAsync(assetId.OrganizationId, assetId.ProjectId);

            // If the task is already completed (cached) and succeeded, we can get the result synchronously
            if (roleTask.IsCompleted && !roleTask.IsFaulted && !roleTask.IsCanceled)
            {
                var role = roleTask.Result;
                return role.CanEdit();
            }

            // If not cached yet, faulted, or canceled, assume no permission (conservative approach)
            // The UI should have already loaded permissions by the time editing is attempted
            return false;
        }

        string GetUserFriendlyErrorMessage(Exception ex, EditField field)
        {
            var fieldName = field.ToString();

            if (ex.Message.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
            {
                return $"You don't have permission to edit {fieldName}";
            }

            if (ex.Message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            {
                return $"Network error while saving {fieldName}. Please check your connection and try again.";
            }

            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return $"Asset not found. It may have been deleted.";
            }

            if (ex.Message.Contains("conflict", StringComparison.OrdinalIgnoreCase))
            {
                return $"Conflict while saving {fieldName}. The asset may have been modified by another user.";
            }

            // Generic fallback: use short message, not full exception payload
            var shortMessage = Utilities.GetUserFacingErrorMessage(ex);
            return string.IsNullOrEmpty(shortMessage) ? $"Failed to save {fieldName}." : $"Failed to save {fieldName}: {shortMessage}";
        }
    }
}
