using System;

namespace Unity.AssetManager.Editor
{
    class ExternalLinkClickedEvent : IBaseEvent
    {
        public enum ExternalLinkType
        {
            OpenDashboard,
            OpenAsset
        }

        [Serializable]
#if UNITY_2023_2_OR_NEWER
        internal class ExternalLinkClickedEventData : IAnalytic.IData
#else
        internal class ExternalLinkClickedEventData : BaseEventData
#endif
        {
            public ExternalLinkType ExternalLinkTypeLinkType;
        }

        static string s_EventName = $"{AnalyticsSender.s_EventPrefix}ExternalLinkClicked";
        static int s_EventVersion = 1;

        public string EventName => s_EventName;
        public int EventVersion => s_EventVersion;

        ExternalLinkClickedEventData m_Data;

        internal ExternalLinkClickedEvent(ExternalLinkType externalLinkType)
        {
            m_Data = new ExternalLinkClickedEventData()
            {
                ExternalLinkTypeLinkType = externalLinkType
            };
        }

#if UNITY_2023_2_OR_NEWER
        [AnalyticInfo(eventName:s_EventName, vendorKey:AnalyticsSender.s_VendorKey, version:s_EventVersion, maxEventsPerHour:1000,maxNumberOfElements:1000)]
        internal class ExternalLinkClickedEventAnalytic : IAnalytic
        {
            ExternalLinkClickedEventData m_Data;

            public ExternalLinkClickedEventAnalytic(ExternalLinkClickedEventData data)
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
            return new ExternalLinkClickedEventAnalytic(m_Data);
        }
#else
        public BaseEventData EventData => m_Data;
#endif
    }
}
