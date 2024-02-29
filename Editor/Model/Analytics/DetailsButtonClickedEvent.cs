using System;

namespace Unity.AssetManager.Editor
{
    class DetailsButtonClickedEvent : IBaseEvent
    {
        public enum ButtonType
        {
            Import,
            Reimport,
            Show,
            Remove
        }

        [Serializable]
#if UNITY_2023_2_OR_NEWER
        internal class DetailsButtonClickedEventData : IAnalytic.IData
#else
        internal class DetailsButtonClickedEventData : BaseEventData
#endif
        {
            public ButtonType ButtonType;
        }

        static string s_EventName = $"{AnalyticsSender.s_EventPrefix}DetailsButtonClicked";
        static int s_EventVersion = 1;

        public string EventName => s_EventName;
        public int EventVersion => s_EventVersion;

        DetailsButtonClickedEventData m_Data;

        internal DetailsButtonClickedEvent(ButtonType buttonType)
        {
            m_Data = new DetailsButtonClickedEventData()
            {
                ButtonType = buttonType
            };
        }

#if UNITY_2023_2_OR_NEWER
        [AnalyticInfo(eventName:s_EventName, vendorKey:AnalyticsSender.s_VendorKey, version:s_EventVersion, maxEventsPerHour:1000,maxNumberOfElements:1000)]
        internal class DetailsButtonClickedEventAnalytic : IAnalytic
        {
            DetailsButtonClickedEventData m_Data;

            public DetailsButtonClickedEventAnalytic(DetailsButtonClickedEventData data)
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
            return new DetailsButtonClickedEventAnalytic(m_Data);
        }
#else
        public BaseEventData EventData => m_Data;
#endif
    }
}
