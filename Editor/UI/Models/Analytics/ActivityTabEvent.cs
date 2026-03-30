using System;
using UnityEngine.Analytics;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Analytics event for tracking Activity tab usage in the Asset Manager Inspector.
    /// Tracks when users view the activity history and load more entries.
    /// </summary>
    class ActivityTabEvent : IBaseEvent
    {
        [Serializable]
#if UNITY_2023_2_OR_NEWER
        internal class ActivityTabEventData : IAnalytic.IData
#else
        internal class ActivityTabEventData : BaseEventData
#endif
        {
            /// <summary>
            /// Action performed: Viewed or LoadedMore.
            /// </summary>
            public string Action;

            /// <summary>
            /// Asset ID being viewed.
            /// </summary>
            public string AssetId;

            /// <summary>
            /// Number of history entries currently displayed.
            /// </summary>
            public int EntryCount;
        }

        internal const string k_EventName = AnalyticsSender.EventPrefix + "ActivityTab";
        internal const int k_EventVersion = 1;

        public string EventName => k_EventName;
        public int EventVersion => k_EventVersion;

        ActivityTabEventData m_Data;

        internal ActivityTabEvent(string action, string assetId, int entryCount)
        {
            m_Data = new ActivityTabEventData
            {
                Action = action ?? "",
                AssetId = assetId ?? "",
                EntryCount = entryCount
            };
        }

#if UNITY_2023_2_OR_NEWER
        [AnalyticInfo(eventName:k_EventName, vendorKey:AnalyticsSender.VendorKey, version:k_EventVersion, maxEventsPerHour:1000, maxNumberOfElements:1000)]
        class ActivityTabEventAnalytic : IAnalytic
        {
            ActivityTabEventData m_Data;

            public ActivityTabEventAnalytic(ActivityTabEventData data)
            {
                m_Data = data;
            }

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                error = null;
                data = m_Data;
                return data != null;
            }
        }

        public IAnalytic GetAnalytic()
        {
            return new ActivityTabEventAnalytic(m_Data);
        }
#else
        public BaseEventData EventData => m_Data;
#endif
    }
}
