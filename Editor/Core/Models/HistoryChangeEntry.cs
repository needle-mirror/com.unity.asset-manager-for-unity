using System;
using System.Collections.Generic;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Represents a single change point in asset update history (per snapshot), with per-field before/after changes.
    /// </summary>
    [Serializable]
    class HistoryChangeEntry
    {
        public int SequenceNumber { get; }
        public DateTime Updated { get; }
        public string UpdatedBy { get; }
        public IReadOnlyList<HistoryFieldChange> Changes { get; }

        public HistoryChangeEntry(int sequenceNumber, DateTime updated, string updatedBy, List<HistoryFieldChange> changes)
        {
            SequenceNumber = sequenceNumber;
            Updated = updated;
            UpdatedBy = updatedBy ?? string.Empty;
            Changes = changes ?? new List<HistoryFieldChange>();
        }
    }
}
