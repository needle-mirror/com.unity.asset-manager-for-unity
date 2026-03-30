using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Upload.Editor;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Shared utilities for upload-tab field containers that compare an asset's current field
    /// value against the value recorded in its <see cref="ImportedAssetInfo"/> to determine
    /// whether the field has been locally edited since the last import.
    /// </summary>
    static class UploadFieldUtils
    {
        /// <summary>
        /// Returns true when at least one asset in <paramref name="selection"/> has a current
        /// field value that differs from the value stored in its <see cref="ImportedAssetInfo"/>.
        /// <para>
        /// The asset ID is resolved via
        /// <see cref="UploadAssetData.ExistingAssetIdentifier"/> when available, falling back to
        /// <see cref="BaseAssetData.Identifier"/>. When <paramref name="getImportedAssetInfo"/>
        /// returns null for an asset, <paramref name="getImported"/> is called with null, so
        /// callers should null-guard the argument (e.g.
        /// <c>info =&gt; info?.AssetData?.Description</c>).
        /// </para>
        /// </summary>
        /// <param name="selection">The currently selected assets to check.</param>
        /// <param name="getImportedAssetInfo">Resolves an <see cref="ImportedAssetInfo"/> by asset ID.</param>
        /// <param name="getCurrent">Extracts the live field value from an asset.</param>
        /// <param name="getImported">Extracts the reference field value from an <see cref="ImportedAssetInfo"/> (may receive null).</param>
        /// <param name="areEqual">
        /// Optional equality predicate. Defaults to <see cref="EqualityComparer{T}.Default"/>
        /// (ordinal string equality for <c>string</c>). Pass a custom predicate for sequence
        /// fields, e.g. <c>(x, y) =&gt; x.SequenceEqual(y)</c> for tags.
        /// </param>
        public static bool IsFieldEdited<T>(
            IEnumerable<BaseAssetData> selection,
            Func<string, ImportedAssetInfo> getImportedAssetInfo,
            Func<BaseAssetData, T> getCurrent,
            Func<ImportedAssetInfo, T> getImported,
            Func<T, T, bool> areEqual = null)
        {
            var equalityCheck = areEqual ?? ((a, b) => EqualityComparer<T>.Default.Equals(a, b));

            foreach (var asset in selection ?? Enumerable.Empty<BaseAssetData>())
            {
                var assetId = (asset as UploadAssetData)?.ExistingAssetIdentifier?.AssetId
                              ?? asset.Identifier.AssetId;
                var importedInfo = getImportedAssetInfo?.Invoke(assetId);

                var current = getCurrent(asset);
                var imported = getImported(importedInfo);

                if (!equalityCheck(imported, current))
                    return true;
            }

            return false;
        }
    }
}

