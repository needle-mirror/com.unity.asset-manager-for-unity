using System;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    class FilterSearchEvent : IBaseEvent
    {
        [Serializable]
#if UNITY_2023_2_OR_NEWER
        internal class FilterSearchEventData : IAnalytic.IData
#else
        internal class FilterSearchEventData : BaseEventData
#endif
        {
            public string FilterName;
            public string FilterValue;
        }

        static string s_EventName = $"{AnalyticsSender.s_EventPrefix}FilterSearch";
        static int s_EventVersion = 1;

        public string EventName => s_EventName;
        public int EventVersion => s_EventVersion;

        FilterSearchEventData m_Data;

        internal FilterSearchEvent(string filterName, string filterValue)
        {
            m_Data = new FilterSearchEventData()
            {
                FilterName = filterName,
                FilterValue = filterValue
            };
        }

#if UNITY_2023_2_OR_NEWER
        [AnalyticInfo(eventName:s_EventName, vendorKey:AnalyticsSender.s_VendorKey, version:s_EventVersion, maxEventsPerHour:1000,maxNumberOfElements:1000)]
        internal class FilterSearchEventAnalytic : IAnalytic
        {
            FilterSearchEventData m_Data;

            public FilterSearchEventAnalytic(FilterSearchEventData data)
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
            return new FilterSearchEventAnalytic(m_Data);
        }
#else
        public BaseEventData EventData => m_Data;
#endif
    }
}
