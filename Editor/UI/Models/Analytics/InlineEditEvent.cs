using System;
using UnityEngine.Analytics;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Analytics event for tracking inline metadata editing operations.
    /// Captures complete edit session including context, outcome, and performance metrics.
    /// </summary>
    class InlineEditEvent : IBaseEvent
    {
        [Serializable]
#if UNITY_2023_2_OR_NEWER
        internal class InlineEditEventData : IAnalytic.IData
#else
        internal class InlineEditEventData : BaseEventData
#endif
        {
            /// <summary>
            /// Field being edited: Name, Description, Status, Tags, Custom, or Dependencies.
            /// </summary>
            public string EditField;

            /// <summary>
            /// For custom metadata fields, the specific type (Text, Number, Boolean, etc.).
            /// Empty string for standard fields.
            /// </summary>
            public string CustomMetadataType;

            /// <summary>
            /// Whether this is multi-asset editing (true) or single-asset (false).
            /// </summary>
            public bool IsMultiAsset;

            /// <summary>
            /// Number of assets being edited.
            /// </summary>
            public int AssetCount;

            /// <summary>
            /// Edit outcome: Completed, Cancelled, Failed, or PartialFailure.
            /// </summary>
            public string Outcome;

            /// <summary>
            /// Time elapsed from edit start to end in milliseconds.
            /// </summary>
            public long ElapsedTimeMs;

            /// <summary>
            /// Number of assets that saved successfully.
            /// </summary>
            public int SucceededCount;

            /// <summary>
            /// Whether the value actually changed from the original.
            /// </summary>
            public bool ValueChanged;

            /// <summary>
            /// Whether selected assets had different initial values (multi-asset only).
            /// </summary>
            public bool HadMixedValues;

            /// <summary>
            /// Number of assets that failed to save.
            /// </summary>
            public int FailedCount;

            /// <summary>
            /// Error category for failures: Permission, Network, NotFound, Conflict, Validation, Other.
            /// Empty string if no error.
            /// </summary>
            public string ErrorCategory;
        }

        internal const string k_EventName = AnalyticsSender.EventPrefix + "InlineEdit";
        internal const int k_EventVersion = 1;

        public string EventName => k_EventName;
        public int EventVersion => k_EventVersion;

        InlineEditEventData m_Data;

        internal InlineEditEvent(
            string editField,
            string customMetadataType,
            bool isMultiAsset,
            int assetCount,
            string outcome,
            long elapsedTimeMs,
            int succeededCount,
            bool valueChanged,
            bool hadMixedValues,
            int failedCount,
            string errorCategory)
        {
            m_Data = new InlineEditEventData
            {
                EditField = editField ?? "",
                CustomMetadataType = customMetadataType ?? "",
                IsMultiAsset = isMultiAsset,
                AssetCount = assetCount,
                Outcome = outcome ?? "",
                ElapsedTimeMs = elapsedTimeMs,
                SucceededCount = succeededCount,
                ValueChanged = valueChanged,
                HadMixedValues = hadMixedValues,
                FailedCount = failedCount,
                ErrorCategory = errorCategory ?? ""
            };
        }

#if UNITY_2023_2_OR_NEWER
        [AnalyticInfo(eventName:k_EventName, vendorKey:AnalyticsSender.VendorKey, version:k_EventVersion, maxEventsPerHour:1000, maxNumberOfElements:1000)]
        class InlineEditEventAnalytic : IAnalytic
        {
            InlineEditEventData m_Data;

            public InlineEditEventAnalytic(InlineEditEventData data)
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
            return new InlineEditEventAnalytic(m_Data);
        }
#else
        public BaseEventData EventData => m_Data;
#endif
    }
}
