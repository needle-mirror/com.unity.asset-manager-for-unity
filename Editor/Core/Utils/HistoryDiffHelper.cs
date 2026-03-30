using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// Builds change entries from a list of update history snapshots (first = created, then diffs vs previous).
    /// </summary>
    static class HistoryDiffHelper
    {
        public const string FieldName = "Name";
        public const string FieldDescription = "Description";
        public const string FieldTags = "Tags";
        public const string FieldCreated = "Created";

        /// <summary>
        /// Produces one HistoryChangeEntry per snapshot. First snapshot is treated as "Created" (all set fields with OldValue = null);
        /// subsequent snapshots are diffed against the previous (Name, Description, Tags, Metadata per-key) with before/after values.
        /// </summary>
        public static List<HistoryChangeEntry> FromSnapshots(IReadOnlyList<AssetUpdateHistorySnapshot> snapshots)
        {
            var result = new List<HistoryChangeEntry>();
            if (snapshots == null || snapshots.Count == 0)
                return result;

            for (var i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                var changes = new List<HistoryFieldChange>();

                if (i == 0)
                {
                    // First snapshot: one HistoryFieldChange per set field with OldValue = null
                    if (!string.IsNullOrEmpty(snapshot.Name))
                        changes.Add(new HistoryFieldChange(FieldName, null, snapshot.Name));
                    if (!string.IsNullOrEmpty(snapshot.Description))
                        changes.Add(new HistoryFieldChange(FieldDescription, null, snapshot.Description));
                    if (snapshot.Tags != null && snapshot.Tags.Count > 0)
                        changes.Add(new HistoryFieldChange(FieldTags, null, string.Join(", ", snapshot.Tags)));
                    if (snapshot.Metadata != null)
                    {
                        foreach (var kvp in snapshot.Metadata)
                            changes.Add(new HistoryFieldChange(kvp.Key, null, kvp.Value ?? string.Empty));
                    }
                    if (changes.Count == 0)
                        changes.Add(new HistoryFieldChange(FieldCreated, null, string.Empty));
                }
                else
                {
                    var prev = snapshots[i - 1];
                    if (!string.Equals(snapshot.Name ?? string.Empty, prev.Name ?? string.Empty, StringComparison.Ordinal))
                        changes.Add(new HistoryFieldChange(FieldName, prev.Name ?? string.Empty, snapshot.Name ?? string.Empty));
                    if (!string.Equals(snapshot.Description ?? string.Empty, prev.Description ?? string.Empty, StringComparison.Ordinal))
                        changes.Add(new HistoryFieldChange(FieldDescription, prev.Description ?? string.Empty, snapshot.Description ?? string.Empty));
                    if (!TagsEqual(snapshot.Tags, prev.Tags))
                    {
                        var prevTags = prev.Tags != null ? string.Join(", ", prev.Tags) : string.Empty;
                        var currTags = snapshot.Tags != null ? string.Join(", ", snapshot.Tags) : string.Empty;
                        changes.Add(new HistoryFieldChange(FieldTags, prevTags, currTags));
                    }
                    AddMetadataChanges(snapshot.Metadata, prev.Metadata, changes);
                }

                result.Add(new HistoryChangeEntry(snapshot.SequenceNumber, snapshot.Updated, snapshot.UpdatedBy, changes));
            }

            return result;
        }

        static bool TagsEqual(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            var setA = new HashSet<string>(a ?? Array.Empty<string>());
            var setB = new HashSet<string>(b ?? Array.Empty<string>());
            return setA.SetEquals(setB);
        }

        static void AddMetadataChanges(IReadOnlyDictionary<string, string> curr, IReadOnlyDictionary<string, string> prev, List<HistoryFieldChange> changes)
        {
            var c = curr ?? new Dictionary<string, string>();
            var p = prev ?? new Dictionary<string, string>();
            var allKeys = c.Keys.Union(p.Keys).ToHashSet();
            foreach (var key in allKeys)
            {
                var hasCurr = TryGetMetadataValue(c, key, out var currVal);
                var hasPrev = TryGetMetadataValue(p, key, out var prevVal);
                if (!hasCurr && !hasPrev) continue;
                if (hasCurr && !hasPrev)
                    changes.Add(new HistoryFieldChange(key, null, currVal ?? string.Empty));
                else if (!hasCurr && hasPrev)
                    changes.Add(new HistoryFieldChange(key, prevVal ?? string.Empty, null));
                else
                {
                    try
                    {
                        if (!string.Equals(currVal ?? string.Empty, prevVal ?? string.Empty, StringComparison.Ordinal))
                            changes.Add(new HistoryFieldChange(key, prevVal ?? string.Empty, currVal ?? string.Empty));
                    }
                    catch (Exception ex)
                    {
                        Utilities.DevLogError($"Error comparing metadata key '{key}': {ex.Message}");
                        changes.Add(new HistoryFieldChange(key, prevVal ?? string.Empty, currVal ?? string.Empty));
                    }
                }
            }
        }

        static bool TryGetMetadataValue(IReadOnlyDictionary<string, string> dict, string key, out string value)
        {
            value = null;
            if (dict == null) return false;
            try
            {
                return dict.TryGetValue(key, out value);
            }
            catch (Exception ex)
            {
                Utilities.DevLogError($"Error accessing metadata key '{key}': {ex.Message}");
                return false;
            }
        }
    }
}
