using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Analytics;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    abstract class BaseEventData{}

    interface IBaseEvent
    {
        string EventName { get; }
        int EventVersion { get; }

#if UNITY_2023_2_OR_NEWER
        IAnalytic GetAnalytic();
#else
        BaseEventData EventData { get; }
#endif
    }

    static class AnalyticsSender
    {
        public static readonly int k_MaxEventsPerHour = 1000;
        public static readonly int k_MaxNumberOfElements = 1000;

        // Vendor key must start with unity.
        public static readonly string s_VendorKey = "unity.asset-explorer";
        public static readonly string s_EventPrefix = "assetManager";

        internal static AnalyticsResult SendEvent(IBaseEvent aEvent)
        {
#if !UNITY_2023_2_OR_NEWER
            var register = EditorAnalytics.RegisterEventWithLimit(aEvent.EventName, k_MaxEventsPerHour,
                k_MaxNumberOfElements, s_VendorKey, aEvent.EventVersion);

            // This is one here for each event, so that if the call fails we don't try to send the event for nothing
            if (register != AnalyticsResult.Ok)
                return register;

            return EditorAnalytics.SendEventWithLimit(aEvent.EventName, aEvent.EventData, aEvent.EventVersion);
#else
            var analytic = aEvent.GetAnalytic();
            return EditorAnalytics.SendAnalytic(analytic);
#endif
        }
    }
}
