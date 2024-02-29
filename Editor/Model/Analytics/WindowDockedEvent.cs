using System;

namespace Unity.AssetManager.Editor
{
    class WindowDockedEvent : IBaseEvent
    {
        [Serializable]
#if UNITY_2023_2_OR_NEWER
        internal class WindowDockedEventData : IAnalytic.IData
#else
        internal class WindowDockedEventData : BaseEventData
#endif
        {
            public bool IsDocked;
        }

        static string s_EventName = $"{AnalyticsSender.s_EventPrefix}WindowDocked";
        static int s_EventVersion = 1;

        public string EventName => s_EventName;
        public int EventVersion => s_EventVersion;

        WindowDockedEventData m_Data;

        internal WindowDockedEvent(bool isDocked)
        {
            m_Data = new WindowDockedEventData()
            {
                IsDocked = isDocked
            };
        }

#if UNITY_2023_2_OR_NEWER
        [AnalyticInfo(eventName:s_EventName, vendorKey:AnalyticsSender.s_VendorKey, version:s_EventVersion, maxEventsPerHour:1000,maxNumberOfElements:1000)]
        internal class WindowDockedEventAnalytic : IAnalytic
        {
            WindowDockedEventData m_Data;

            public WindowDockedEventAnalytic(WindowDockedEventData data)
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
            return new WindowDockedEventAnalytic(m_Data);
        }
#else
        public BaseEventData EventData => m_Data;
#endif
    }
}
