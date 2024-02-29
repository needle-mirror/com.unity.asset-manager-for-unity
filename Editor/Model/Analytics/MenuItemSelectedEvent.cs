using System;
using UnityEngine.Serialization;

namespace Unity.AssetManager.Editor
{
    class MenuItemSelectedEvent: IBaseEvent
    {
        public enum MenuItemType
        {
            Refresh,
            GoToDashboard,
            ProjectSettings,
            Preferences
        }

        [Serializable]
#if UNITY_2023_2_OR_NEWER
        internal class MenuItemSelectedEventData : IAnalytic.IData
#else
        internal class MenuItemSelectedEventData : BaseEventData
#endif
        {
            [FormerlySerializedAs("MenuItemSelectedd")]
            [FormerlySerializedAs("MenuItemType")]
            public MenuItemType MenuItemSelected;
        }

        static string s_EventName = $"{AnalyticsSender.s_EventPrefix}MenuItemSelected";
        static int s_EventVersion = 1;

        public string EventName => s_EventName;
        public int EventVersion => s_EventVersion;

        MenuItemSelectedEventData m_Data;

        internal MenuItemSelectedEvent(MenuItemType menuItemType)
        {
            m_Data = new MenuItemSelectedEventData()
            {
                MenuItemSelected = menuItemType
            };
        }

#if UNITY_2023_2_OR_NEWER
        [AnalyticInfo(eventName:s_EventName, vendorKey:AnalyticsSender.s_VendorKey, version:s_EventVersion, maxEventsPerHour:1000,maxNumberOfElements:1000)]
        internal class MenuItemSelectedEventAnalytic : IAnalytic
        {
            MenuItemSelectedEventData m_Data;

            public MenuItemSelectedEventAnalytic(MenuItemSelectedEventData data)
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
            return new MenuItemSelectedEventAnalytic(m_Data);
        }
#else
        public BaseEventData EventData => m_Data;
#endif
    }
}

