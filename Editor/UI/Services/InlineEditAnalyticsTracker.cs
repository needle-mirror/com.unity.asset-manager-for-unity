using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Outcome of an inline edit operation for analytics.
    /// </summary>
    enum EditOutcome
    {
        /// <summary>
        /// Edit completed successfully for all assets.
        /// </summary>
        Completed,

        /// <summary>
        /// Edit was cancelled by the user.
        /// </summary>
        Cancelled,

        /// <summary>
        /// Edit failed for all assets.
        /// </summary>
        Failed,

        /// <summary>
        /// Edit succeeded for some assets but failed for others (multi-asset only).
        /// </summary>
        PartialFailure
    }

    /// <summary>
    /// Represents an active edit session for tracking analytics.
    /// Captures context about the edit operation and measures elapsed time.
    /// </summary>
    class EditSession
    {
        public string SessionId { get; }
        public string EditField { get; }
        public string CustomMetadataType { get; }
        public bool IsMultiAsset { get; }
        public int AssetCount { get; }
        public bool HadMixedValues { get; }
        public Stopwatch Stopwatch { get; }

        public EditSession(string editField, string customType, bool isMulti, int count, bool hadMixed)
        {
            SessionId = Guid.NewGuid().ToString();
            EditField = editField;
            CustomMetadataType = customType ?? "";
            IsMultiAsset = isMulti;
            AssetCount = count;
            HadMixedValues = hadMixed;
            Stopwatch = Stopwatch.StartNew();
        }
    }

    /// <summary>
    /// Tracks analytics for inline metadata editing operations.
    /// Provides centralized session management, error categorization, and event sending.
    /// </summary>
    static class InlineEditAnalyticsTracker
    {
        static readonly Dictionary<string, EditSession> s_ActiveSessions = new Dictionary<string, EditSession>();

        /// <summary>
        /// Begin tracking an inline edit session.
        /// Call this when the user starts editing a field.
        /// </summary>
        /// <param name="editField">Field being edited (e.g., "Name", "Description", "Status", "Tags", "Custom")</param>
        /// <param name="customType">For custom metadata, the field type (e.g., "Text", "Number"). Null for standard fields.</param>
        /// <param name="isMulti">True if editing multiple assets, false for single asset</param>
        /// <param name="count">Number of assets being edited</param>
        /// <param name="hadMixed">True if selected assets had different initial values</param>
        /// <returns>EditSession object to pass to EndEdit</returns>
        public static EditSession BeginEdit(string editField, string customType, bool isMulti, int count, bool hadMixed)
        {
            var session = new EditSession(editField, customType, isMulti, count, hadMixed);
            s_ActiveSessions[session.SessionId] = session;
            return session;
        }

        /// <summary>
        /// End an edit session and send analytics event.
        /// Call this when the edit completes, is cancelled, or fails.
        /// </summary>
        /// <param name="session">Session created by BeginEdit</param>
        /// <param name="outcome">Outcome of the edit operation</param>
        /// <param name="valueChanged">Whether the value actually changed from the original</param>
        /// <param name="succeededCount">Number of assets saved successfully (0 for cancelled)</param>
        /// <param name="failedCount">Number of assets that failed to save (0 for completed/cancelled)</param>
        /// <param name="errorCategory">Error category for failures (null if no error)</param>
        public static void EndEdit(
            EditSession session,
            EditOutcome outcome,
            bool valueChanged,
            int succeededCount,
            int failedCount,
            string errorCategory)
        {
            if (session == null)
                return;

            session.Stopwatch.Stop();

            // Remove from active sessions
            s_ActiveSessions.Remove(session.SessionId);

            // Send analytics event
            var analyticsEvent = new InlineEditEvent(
                editField: session.EditField,
                customMetadataType: session.CustomMetadataType,
                isMultiAsset: session.IsMultiAsset,
                assetCount: session.AssetCount,
                outcome: outcome.ToString(),
                elapsedTimeMs: session.Stopwatch.ElapsedMilliseconds,
                succeededCount: succeededCount,
                valueChanged: valueChanged,
                hadMixedValues: session.HadMixedValues,
                failedCount: failedCount,
                errorCategory: errorCategory ?? "");

            AnalyticsSender.SendEvent(analyticsEvent);
        }

        /// <summary>
        /// Track activity tab interactions.
        /// Call this when the Activity tab is viewed or when the user loads more entries.
        /// </summary>
        /// <param name="action">Action performed: "Viewed" or "LoadedMore"</param>
        /// <param name="assetId">Asset ID being viewed</param>
        /// <param name="entryCount">Number of history entries currently displayed</param>
        public static void TrackActivityTab(string action, string assetId, int entryCount)
        {
            var analyticsEvent = new ActivityTabEvent(action, assetId ?? "", entryCount);
            AnalyticsSender.SendEvent(analyticsEvent);
        }

        /// <summary>
        /// Categorize exception into error category for analytics.
        /// Maps exception types and messages to standard categories.
        /// </summary>
        /// <param name="ex">Exception to categorize</param>
        /// <returns>Error category: Permission, Network, NotFound, Conflict, Validation, or Other</returns>
        public static string CategorizeException(Exception ex)
        {
            if (ex == null)
                return null;

            // Check if it's a ServiceException with HTTP status code
            var serviceExceptionInfo = ServiceExceptionHelper.GetServiceExceptionInfo(ex);
            if (serviceExceptionInfo?.StatusCode != null)
            {
                return CategorizeHttpStatus(serviceExceptionInfo.StatusCode.Value);
            }

            return CategorizeErrorMessage(ex.Message);
        }

        /// <summary>
        /// Categorize HTTP status code into error category.
        /// </summary>
        static string CategorizeHttpStatus(HttpStatusCode statusCode)
        {
            switch (statusCode)
            {
                case HttpStatusCode.Unauthorized:
                case HttpStatusCode.Forbidden:
                    return "Permission";

                case HttpStatusCode.NotFound:
                case HttpStatusCode.Gone:
                    return "NotFound";

                case HttpStatusCode.Conflict:
                case HttpStatusCode.PreconditionFailed:
                    return "Conflict";

                case HttpStatusCode.BadRequest:
                case HttpStatusCode.UnprocessableEntity:
                    return "Validation";

                case HttpStatusCode.RequestTimeout:
                case HttpStatusCode.GatewayTimeout:
                case HttpStatusCode.ServiceUnavailable:
                case HttpStatusCode.BadGateway:
                    return "Network";

                default:
                    return "Other";
            }
        }

        /// <summary>
        /// Categorize error message into error category.
        /// </summary>
        static string CategorizeErrorMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "Other";

            var lower = message.ToLowerInvariant();

            if (lower.Contains("permission") || lower.Contains("forbidden") || lower.Contains("unauthorized"))
                return "Permission";

            if (lower.Contains("network") || lower.Contains("timeout") || lower.Contains("connection"))
                return "Network";

            if (lower.Contains("not found") || lower.Contains("does not exist"))
                return "NotFound";

            if (lower.Contains("conflict") || lower.Contains("version"))
                return "Conflict";

            if (lower.Contains("validation") || lower.Contains("invalid"))
                return "Validation";

            return "Other";
        }

        /// <summary>
        /// Determine outcome from BatchSaveResult.
        /// </summary>
        /// <param name="result">Result from multi-asset save operation</param>
        /// <returns>Completed, Failed, or PartialFailure</returns>
        public static EditOutcome DetermineOutcome(BatchSaveResult result)
        {
            if (result.AllSucceeded)
                return EditOutcome.Completed;
            if (result.AllFailed)
                return EditOutcome.Failed;
            return EditOutcome.PartialFailure;
        }

        /// <summary>
        /// Get first error category from BatchSaveResult failures.
        /// Analyzes error messages to categorize the failure.
        /// </summary>
        /// <param name="result">Result from multi-asset save operation</param>
        /// <returns>Error category or null if no failures</returns>
        public static string GetErrorCategoryFromBatchResult(BatchSaveResult result)
        {
            if (result.FailedAssets.Count == 0)
                return null;

            return CategorizeErrorMessage(result.FailedAssets[0].ErrorMessage);
        }
    }
}
