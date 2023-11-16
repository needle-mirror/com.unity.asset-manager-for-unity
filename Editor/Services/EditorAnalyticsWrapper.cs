using System;
using Unity.AssetManager.Editor.Model.Analytics;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine.Analytics;

namespace Unity.AssetManager.Editor
{
    internal interface IEditorAnalyticsWrapper : IService
    {
#if !UNITY_2023_2_OR_NEWER
        AnalyticsResult RegisterEventWithLimit(
            string eventName,
            int maxEventPerHour,
            int maxItems,
            string vendorKey,
            int ver);

        AnalyticsResult SendEventWithLimit(string eventName, object parameters, int ver);
#else
        AnalyticsResult SendAnalytic(IAnalytic analytic);
#endif
    }

    internal class EditorAnalyticsWrapper : BaseService<IEditorAnalyticsWrapper>, IEditorAnalyticsWrapper
    {
#if !UNITY_2023_2_OR_NEWER
        /// <summary>
        ///   <para>This API is used for registering an Editor Analytics event. Note: This API is for internal use only and is likely change in the future. Do not use in user code.</para>
        /// </summary>
        /// <param name="eventName">Name of the event.</param>
        /// <param name="maxEventPerHour">Hourly limit for this event name.</param>
        /// <param name="maxItems">Maximum number of items in this event.</param>
        /// <param name="vendorKey">Vendor key name.</param>
        public AnalyticsResult RegisterEventWithLimit(
            string eventName,
            int maxEventPerHour,
            int maxItems,
            string vendorKey,
            int ver)
        {
            return EditorAnalytics.RegisterEventWithLimit(eventName, maxEventPerHour, maxItems, vendorKey, ver);
        }

        /// <summary>
        ///   <para>This API is used to send an Editor Analytics event. Note: This API is for internal use only and is likely change in the future. Do not use in user code.</para>
        /// </summary>
        /// <param name="eventName">Name of the event.</param>
        /// <param name="parameters">Additional event data.</param>
        /// <param name="ver">Event version number.</param>
        public AnalyticsResult SendEventWithLimit(string eventName, object parameters, int ver)
        {
            return EditorAnalytics.SendEventWithLimit(eventName, parameters, ver);
        }
#else
        public AnalyticsResult SendAnalytic(IAnalytic analytic)
        {
            return EditorAnalytics.SendAnalytic(analytic);
        }
#endif
    }
}