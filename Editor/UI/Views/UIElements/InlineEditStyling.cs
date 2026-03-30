using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Shared styling utilities for inline edit UI elements.
    /// Provides consistent visual feedback for edited fields across entry types.
    /// </summary>
    static class InlineEditStyling
    {
        /// <summary>
        /// Updates the visual styling to indicate whether a field has been edited.
        /// In Upload mode, shows a colored border and edited class when the field differs from its original value.
        /// In other modes, clears the edited styling.
        /// </summary>
        /// <param name="borderLine">The border element to color (can be null).</param>
        /// <param name="field">The field element to apply the edited class to (can be null).</param>
        /// <param name="mode">The current editing mode.</param>
        /// <param name="isEdited">Function that returns true if the current value differs from the original.</param>
        public static void UpdateEditedStyling(
            VisualElement borderLine,
            VisualElement field,
            EditingMode mode,
            Func<bool> isEdited)
        {
            var showEdited = mode == EditingMode.Upload && (isEdited?.Invoke() ?? false);

            if (showEdited)
            {
                if (borderLine != null)
                    borderLine.style.backgroundColor = UssStyle.EditedBorderColor;
                field?.AddToClassList(UssStyle.DetailsPageEntryValueEdited);
            }
            else
            {
                if (borderLine != null)
                    borderLine.style.backgroundColor = Color.clear;
                field?.RemoveFromClassList(UssStyle.DetailsPageEntryValueEdited);
            }
        }
    }
}
