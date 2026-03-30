using System;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Represents a single field change with before/after values for history diff display.
    /// </summary>
    class HistoryFieldChange
    {
        public string FieldName { get; }
        public string OldValue { get; }
        public string NewValue { get; }

        public HistoryFieldChange(string fieldName, string oldValue, string newValue)
        {
            FieldName = fieldName ?? string.Empty;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
