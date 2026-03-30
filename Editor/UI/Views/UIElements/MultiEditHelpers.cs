using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Result of a batch save operation across multiple assets.
    /// </summary>
    class BatchSaveResult
    {
        public int SucceededCount { get; }
        public int FailedCount => FailedAssets.Count;
        public int TotalCount => SucceededCount + FailedCount;
        public bool AllSucceeded => FailedCount == 0;
        public bool AllFailed => SucceededCount == 0 && FailedCount > 0;
        public bool PartialFailure => SucceededCount > 0 && FailedCount > 0;

        /// <summary>
        /// List of (AssetId, ErrorMessage) pairs for failed saves.
        /// </summary>
        public IReadOnlyList<(string AssetId, string ErrorMessage)> FailedAssets { get; }

        public BatchSaveResult(int succeededCount, List<(string AssetId, string ErrorMessage)> failedAssets)
        {
            SucceededCount = succeededCount;
            FailedAssets = failedAssets ?? new List<(string, string)>();
        }

        public static BatchSaveResult Success(int count) =>
            new BatchSaveResult(count, new List<(string, string)>());

        public string GetSummaryMessage()
        {
            if (AllSucceeded)
                return null;

            if (AllFailed)
            {
                var firstError = FailedAssets.FirstOrDefault().ErrorMessage ?? "Unknown error";
                return FailedCount == 1
                    ? $"Save failed: {firstError}"
                    : $"Save failed for all {FailedCount} assets: {firstError}";
            }

            // Partial failure
            var failedIds = string.Join(", ", FailedAssets.Take(3).Select(f => f.AssetId));
            if (FailedAssets.Count > 3)
                failedIds += $" and {FailedAssets.Count - 3} more";

            return $"Saved {SucceededCount} of {TotalCount} assets. Failed: {failedIds}";
        }
    }

    /// <summary>
    /// Helper methods for multi-edit entry classes to reduce code duplication.
    /// </summary>
    static class MultiEditHelpers
    {
        // Conservative limit for multi-edit write operations to avoid rate limiting
        const int k_MaxConcurrentSaves = 10;

        /// <summary>
        /// Creates and attaches a confirmation popup to the specified parent, disposing any existing popup.
        /// </summary>
        public static InlineEditConfirmationPopupContainer SetupConfirmationPopup(
            ref InlineEditConfirmationPopupContainer existingPopup,
            VisualElement parent)
        {
            existingPopup?.Dispose();
            existingPopup = new InlineEditConfirmationPopupContainer();
            parent.Add(existingPopup);
            return existingPopup;
        }

        /// <summary>
        /// Returns true and sets <paramref name="sharedValue"/> to the common element when all items in
        /// <paramref name="values"/> are equal; otherwise returns false and sets
        /// <paramref name="sharedValue"/> to the type default. Returns false for an empty sequence.
        /// </summary>
        public static bool TryGetSharedValue<T>(IEnumerable<T> values, out T sharedValue)
        {
            if (values == null)
            {
                sharedValue = default;
                return false;
            }

            var comparer = EqualityComparer<T>.Default;
            T first = default;
            var hasFirst = false;

            foreach (var v in values)
            {
                if (!hasFirst)
                {
                    first = v;
                    hasFirst = true;
                }
                else if (!comparer.Equals(v, first))
                {
                    sharedValue = default;
                    return false;
                }
            }

            sharedValue = first;
            return hasFirst;
        }

        /// <summary>
        /// Computes the set intersection of all <paramref name="valueSets"/> and sets
        /// <paramref name="hasMixedValues"/> to true when at least one set contains an element
        /// not present in every other set. Returns an empty list for an empty input.
        /// </summary>
        public static List<T> GetIntersection<T>(IEnumerable<IEnumerable<T>> valueSets, out bool hasMixedValues)
        {
            hasMixedValues = false;

            if (valueSets == null)
                return new List<T>();

            List<T> firstList = null;
            HashSet<T> intersection = null;

            foreach (var valueSet in valueSets)
            {
                var current = valueSet?.ToList() ?? new List<T>();

                if (intersection == null)
                {
                    firstList = current;
                    intersection = new HashSet<T>(current);
                }
                else
                {
                    // If current contains elements outside the running intersection, those
                    // elements are unique to this set -> mixed.
                    if (!hasMixedValues)
                        hasMixedValues = current.Any(item => !intersection.Contains(item));

                    var prevCount = intersection.Count;
                    intersection.IntersectWith(current);

                    // If the intersection shrank, the first set (and any prior sets) had
                    // elements not present in current -> mixed.
                    if (!hasMixedValues && intersection.Count < prevCount)
                        hasMixedValues = true;
                }
            }

            if (intersection == null)
                return new List<T>();

            // Re-filter the first list to preserve its original insertion order.
            return firstList.Where(item => intersection.Contains(item)).ToList();
        }

        /// <summary>
        /// Returns true when all assets share the same <see cref="BaseAssetData.StatusFlowId"/>,
        /// or the collection contains fewer than two items.
        /// </summary>
        public static bool AllShareSameStatusFlow(IEnumerable<BaseAssetData> assets)
            => AllShareSameStringProperty(assets, a => a.StatusFlowId);

        /// <summary>
        /// Returns true when all assets share the same <see cref="BaseAssetData.Status"/> value,
        /// or the collection contains fewer than two items.
        /// </summary>
        public static bool AllShareSameStatus(IEnumerable<BaseAssetData> assets)
            => AllShareSameStringProperty(assets, a => a.Status);

        static bool AllShareSameStringProperty(IEnumerable<BaseAssetData> assets, Func<BaseAssetData, string> selector)
        {
            if (assets == null)
                return true;

            string first = null;
            var hasFirst = false;

            foreach (var asset in assets)
            {
                if (!hasFirst)
                {
                    first = selector(asset);
                    hasFirst = true;
                }
                else if (!string.Equals(selector(asset), first, StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Creates the "— Mixed" chip used to indicate that selected assets have
        /// differing values for a multi-value field.
        /// </summary>
        public static Chip CreateMixedValueChip()
        {
            var chip = new Chip("— Mixed", isDismissable: false);
            chip.style.unityFontStyleAndWeight = FontStyle.Italic;
            return chip;
        }

        /// <summary>
        /// Clears <paramref name="target"/> and repopulates it with one <see cref="Chip"/> per
        /// value in <paramref name="values"/>, plus a "— Mixed" chip when
        /// <paramref name="hasMixed"/> is true.
        /// Pass <paramref name="mixedFirst"/> = true to prepend the mixed chip before the value
        /// chips (e.g. the tags read-only display); the default appends it after.
        /// </summary>
        public static void PopulateMixedChipContainer(
            VisualElement target,
            IEnumerable<string> values,
            bool hasMixed,
            bool mixedFirst = false)
        {
            target.Clear();

            if (mixedFirst && hasMixed)
                target.Add(CreateMixedValueChip());

            foreach (var value in values ?? Enumerable.Empty<string>())
                target.Add(new Chip(value));

            if (!mixedFirst && hasMixed)
                target.Add(CreateMixedValueChip());
        }

        /// <summary>
        /// Creates a new <see cref="VisualElement"/> with CSS class
        /// <paramref name="containerClass"/> and populates it using
        /// <see cref="PopulateMixedChipContainer"/>.
        /// </summary>
        public static VisualElement BuildMixedChipContainer(
            IEnumerable<string> values,
            bool hasMixed,
            string containerClass,
            bool mixedFirst = false)
        {
            var container = new VisualElement();
            if (!string.IsNullOrEmpty(containerClass))
                container.AddToClassList(containerClass);
            PopulateMixedChipContainer(container, values, hasMixed, mixedFirst);
            return container;
        }

        /// <summary>
        /// Saves the same field value to multiple assets in parallel with rate limiting.
        /// Attempts all saves and returns a result with successes/failures.
        /// </summary>
        public static async Task<BatchSaveResult> SaveToAllAsync(
            IInlineEditService service,
            IEnumerable<AssetIdentifier> identifiers,
            EditField field,
            object value,
            CancellationToken token = default)
        {
            if (service == null)
                return new BatchSaveResult(0, new List<(string, string)> { ("", "Inline edit service not configured") });

            var identifierList = identifiers.ToList();
            if (identifierList.Count == 0)
                return BatchSaveResult.Success(0);

            var succeeded = 0;
            var failed = new ConcurrentBag<(string AssetId, string ErrorMessage)>();

            await TaskUtils.RunAllTasksInQueue(
                identifierList,
                async identifier =>
                {
                    var edit = new AssetFieldEdit(identifier, field, value);
                    var result = await service.SaveFieldAsync(edit, token);
                    if (result.Success)
                        Interlocked.Increment(ref succeeded);
                    else
                        failed.Add((identifier.AssetId, result.ErrorMessage));
                },
                k_MaxConcurrentSaves);

            return new BatchSaveResult(succeeded, failed.ToList());
        }

        /// <summary>
        /// Saves different field values to multiple assets (one value per asset) in parallel with rate limiting.
        /// Attempts all saves and returns a result with successes/failures.
        /// </summary>
        public static async Task<BatchSaveResult> SaveToAllAsync<T>(
            IInlineEditService service,
            IReadOnlyList<AssetIdentifier> identifiers,
            EditField field,
            IReadOnlyList<T> values,
            CancellationToken token = default)
        {
            if (service == null)
                return new BatchSaveResult(0, new List<(string, string)> { ("", "Inline edit service not configured") });

            var count = Math.Min(identifiers.Count, values.Count);
            if (count == 0)
                return BatchSaveResult.Success(0);

            var succeeded = 0;
            var failed = new ConcurrentBag<(string AssetId, string ErrorMessage)>();

            // Create indexed pairs for parallel processing
            var indexedItems = Enumerable.Range(0, count).ToList();

            await TaskUtils.RunAllTasksInQueue(
                indexedItems,
                async index =>
                {
                    var edit = new AssetFieldEdit(identifiers[index], field, values[index]);
                    var result = await service.SaveFieldAsync(edit, token);
                    if (result.Success)
                        Interlocked.Increment(ref succeeded);
                    else
                        failed.Add((identifiers[index].AssetId, result.ErrorMessage));
                },
                k_MaxConcurrentSaves);

            return new BatchSaveResult(succeeded, failed.ToList());
        }
    }
}
