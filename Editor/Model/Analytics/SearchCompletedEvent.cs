using System;

namespace Unity.AssetManager.Editor
{
    class SearchAttemptEvent : IBaseEvent
    {
        [Serializable]
#if UNITY_2023_2_OR_NEWER
        internal class SearchAttemptEventData : IAnalytic.IData
#else
        internal class SearchAttemptEventData : BaseEventData
#endif
        {
            public int KeywordCount;
        }

        static string s_EventName = $"{AnalyticsSender.s_EventPrefix}SearchAttempt";
        static int s_EventVersion = 1;

        public string EventName => s_EventName;
        public int EventVersion => s_EventVersion;

        SearchAttemptEventData m_Data;

        internal SearchAttemptEvent(int count)
        {
            m_Data = new SearchAttemptEventData()
            {
                KeywordCount = count
            };
        }

#if UNITY_2023_2_OR_NEWER
        [AnalyticInfo(eventName:s_EventName, vendorKey:AnalyticsSender.s_VendorKey, version:s_EventVersion, maxEventsPerHour:1000,maxNumberOfElements:1000)]
        internal class SearchAttemptEventAnalytic : IAnalytic
        {
            SearchAttemptEventData m_Data;

            public SearchAttemptEventAnalytic(SearchAttemptEventData data)
            {
                m_Data = data;
            }

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                error = null;
                data = m_Data
                return data != null;
            }
        }

        public IAnalytic GetAnalytic()
        {
            return new SearchAttemptEventAnalytic(m_Data);
        }
#else
        public BaseEventData EventData => m_Data;
#endif
    }
}
