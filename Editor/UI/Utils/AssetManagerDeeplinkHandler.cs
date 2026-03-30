using System;
using System.Collections.Generic;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditorInternal;

namespace Unity.AssetManager.UI.Editor
{
    class AssetManagerDeeplinkHandler
    {
#if UNITY_6000_3_OR_NEWER
        // Expecting URL format: com.unity.editor://editor/unity.assetmanager.ui.editor/{action}/{payload}
        // where {action} is "open-asset" and {payload} is the encoded asset resource url
        [DeeplinkHandler("Unity.AssetManager.UI.Editor")]
        public static void OnDeeplink(Uri uri)
        {
            Utilities.DevLog($"DeeplinkHandler Attribute Received deep link for Unity.AssetManager.UI.Editor namespace: {uri.AbsoluteUri}");
            ProcessUrl(uri.AbsoluteUri);
        }
#endif
        static void ProcessUrl(string url)
        {
            var uri = new Uri(url);
            if (!DeepLinkDispatch.TryGetActionAndPayload(uri, out var action, out var payload))
            {
                var reference = GetActionSegmentFromUri(uri) ?? uri.AbsolutePath ?? uri.AbsoluteUri;
                ShowDeeplinkError(string.Format(L10n.Tr(Constants.DeeplinkActionNotSupportedText), reference));
                return;
            }

            if (!DeepLinkDispatch.TryHandle(action, payload))
            {
                ShowDeeplinkError(L10n.Tr(Constants.DeeplinkInvalidLinkFormatText));
                return;
            }
        }

        /// <summary>
        /// Gets the path segment that would be the action (first segment after the namespace) for use as copyable reference when format is invalid.
        /// </summary>
        static string GetActionSegmentFromUri(Uri uri)
        {
            if (uri?.Segments == null)
                return null;
            var segments = uri.Segments;
            for (var i = 0; i < segments.Length; i++)
            {
                var name = AssetResourceUrlParser.RemoveSegmentDelimiter(segments[i]);
                if (string.IsNullOrEmpty(name))
                    continue;
                if (string.Equals(name, "unity.assetmanager.ui.editor", StringComparison.OrdinalIgnoreCase) && i + 1 < segments.Length)
                    return AssetResourceUrlParser.RemoveSegmentDelimiter(segments[i + 1]);
            }
            return segments.Length > 0 ? AssetResourceUrlParser.RemoveSegmentDelimiter(segments[segments.Length - 1]) : null;
        }

        static void ShowDeeplinkError(string message)
        {
            AssetManagerWindow.Open();
            EditorApplication.delayCall += () =>
            {
                if (ServicesContainer.instance.IsInitialized())
                {
                    var messageManager = ServicesContainer.instance.Resolve<IMessageManager>();
                    messageManager?.SetHelpBoxMessage(new HelpBoxMessage(message,
                        RecommendedAction.None, HelpBoxMessageType.Warning, dismissable: true, category: MessageCategory.Deeplink));
                }
            };
        }
    }

    /// <summary>
    /// Identifies the deep-link action from the URI and dispatches to the handler registered for that action.
    /// Keeps action identification separate from URL processing so new actions can be added without changing the dispatch flow.
    /// </summary>
    static class DeepLinkDispatch
    {
        delegate bool ActionHandler(string payload);

        static readonly Dictionary<string, ActionHandler> s_Handlers = new Dictionary<string, ActionHandler>(StringComparer.OrdinalIgnoreCase)
        {
            { "open-asset", HandleOpenAsset }
        };

        /// <summary>
        /// Finds the first path segment that is a registered action and returns that action name plus the following segment as payload.
        /// </summary>
        internal static bool TryGetActionAndPayload(Uri uri, out string action, out string payload)
        {
            action = null;
            payload = null;
            if (uri?.Segments == null)
                return false;

            var segments = uri.Segments;
            for (var i = 0; i < segments.Length; i++)
            {
                var segmentName = AssetResourceUrlParser.RemoveSegmentDelimiter(segments[i]);
                if (string.IsNullOrEmpty(segmentName) || !s_Handlers.ContainsKey(segmentName))
                    continue;

                action = segmentName;
                payload = i + 1 < segments.Length
                    ? AssetResourceUrlParser.RemoveSegmentDelimiter(segments[i + 1])
                    : null;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Invokes the handler for the given action with the given payload. Returns true if the action was handled successfully.
        /// </summary>
        internal static bool TryHandle(string action, string payload)
        {
            if (string.IsNullOrEmpty(action) || !s_Handlers.TryGetValue(action, out var handler))
                return false;

            return handler(payload);
        }

        static bool HandleOpenAsset(string payload)
        {
            if (string.IsNullOrEmpty(payload))
                return false;

            if (!Uri.TryCreate(Uri.UnescapeDataString(payload), UriKind.Absolute, out var resourceUri))
                return false;

            if (!AssetResourceUrlParser.TryParseAssetResourceUrl(resourceUri, out var orgId, out var projectId, out var assetId, out var version))
                return false;
            if (string.IsNullOrEmpty(orgId) || string.IsNullOrEmpty(projectId))
                return false;

            var assetIdentifier = new AssetIdentifier(orgId, projectId, assetId, version);
            var openAssetHook = new OpenAssetHook(assetIdentifier);
            openAssetHook.OpenAssetManagerWindow();
            return true;
        }
    }

    /// <summary>
    /// Parses a cloud asset manager resource URL by inspecting both query parameters and path segments
    /// so that asset id, version, organization and project can be found regardless of URL structure.
    /// </summary>
    static class AssetResourceUrlParser
    {
        /// <summary>
        /// URL must have at least 3 path segments after the domain: namespace, action, and payload.
        /// </summary>
        internal static bool HasRequiredPathStructure(Uri uri)
        {
            return uri?.Segments != null && uri.Segments.Length >= 4;
        }

        internal static bool TryParseAssetResourceUrl(Uri resourceUri, out string organizationId, out string projectId, out string assetId, out string version)
        {
            organizationId = null;
            projectId = null;
            assetId = null;
            version = null;

            if (!HasRequiredPathStructure(resourceUri))
                return false;

            var query = ParseQueryString(resourceUri);
            var segments = GetPathSegmentValues(resourceUri);

            // Prefer path segments over query for organization and project; query then segments for assetId
            organizationId = GetFirstNonEmpty(
                GetValue(segments, "organizations"),
                GetValue(query, "organizationId"),
                GetValue(query, "organization"));
            projectId = GetFirstNonEmpty(
                GetValue(segments, "projects"),
                GetValue(query, "projectId"),
                GetValue(query, "project"));
            // assetId and version current format is a single param with ':' (e.g. assetId=65ce3df1c444a1fbd1546838:1)
            var assetIdRaw = GetFirstNonEmpty(
                GetValue(query, "assetId"),
                GetValue(segments, "assets"));
            if (string.IsNullOrEmpty(assetIdRaw))
            {
                assetId = null;
                return false;
            }

            ExtractAssetIdAndVersion(assetIdRaw, out assetId, out version);
            // Allow separate version query param or segment to override the version from "id:version" form
            var versionOverride = GetFirstNonEmpty(
                GetValue(segments, "versions"),
                GetValue(segments, "assetVersions"),
                GetValue(query, "assetVersion"),
                GetValue(query, "version"));
            if (!string.IsNullOrEmpty(versionOverride))
                version = versionOverride;

            if (string.IsNullOrEmpty(organizationId) || string.IsNullOrEmpty(projectId) || string.IsNullOrEmpty(assetId) || string.IsNullOrEmpty(version))
                return false;

            return true;
        }

        static Dictionary<string, string> ParseQueryString(Uri uri)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var query = uri.Query;
            if (string.IsNullOrEmpty(query) || query[0] != '?')
                return result;
            foreach (var pair in query.Substring(1).Split('&'))
            {
                var eq = pair.IndexOf('=');
                var key = eq >= 0 ? Uri.UnescapeDataString(pair.Substring(0, eq)) : Uri.UnescapeDataString(pair);
                var value = eq >= 0 && eq < pair.Length - 1 ? Uri.UnescapeDataString(pair.Substring(eq + 1)) : string.Empty;
                if (!string.IsNullOrEmpty(key) && !result.ContainsKey(key))
                    result[key] = value;
            }
            return result;
        }

        /// <summary>
        /// Maps known segment names (e.g. "organizations", "projects") to the following segment value.
        /// </summary>
        static Dictionary<string, string> GetPathSegmentValues(Uri uri)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var segments = uri.Segments;
            for (var i = 0; i < segments.Length - 1; i++)
            {
                var name = RemoveSegmentDelimiter(segments[i]);
                if (string.IsNullOrEmpty(name))
                    continue;
                var value = RemoveSegmentDelimiter(segments[i + 1]);
                if (!string.IsNullOrEmpty(value) && !result.ContainsKey(name))
                    result[name] = value;
            }
            return result;
        }

        internal static int FindSegmentIndex(string[] segments, string segmentName)
        {
            var normalized = segmentName?.Trim('/') ?? string.Empty;
            for (var i = 0; i < segments.Length; i++)
            {
                if (string.Equals(RemoveSegmentDelimiter(segments[i]), normalized, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        internal static string RemoveSegmentDelimiter(string segment)
        {
            return segment?.Trim('/') ?? string.Empty;
        }

        static string GetValue(Dictionary<string, string> dict, string key)
        {
            return dict != null && key != null && dict.TryGetValue(key, out var v) ? v : null;
        }

        static string GetFirstNonEmpty(params string[] values)
        {
            foreach (var v in values)
            {
                if (!string.IsNullOrEmpty(v))
                    return v;
            }
            return null;
        }

        /// <summary>
        /// Splits a value that may be "assetId" or "assetId:version" (single parameter with ':' delimiter)
        /// into id and version. Version is null when no ':' is present.
        /// </summary>
        static void ExtractAssetIdAndVersion(string str, out string id, out string version)
        {
            str = str?.Trim() ?? string.Empty;
            var delimiter = str.IndexOf(':');
            if (delimiter == -1)
            {
                id = str;
                version = null;
            }
            else
            {
                id = str.Substring(0, delimiter);
                version = str.Substring(delimiter + 1);
            }
        }
    }
}