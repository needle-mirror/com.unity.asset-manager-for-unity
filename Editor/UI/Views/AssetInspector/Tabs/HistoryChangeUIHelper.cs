using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Builds the visual tree for a single history field change (before/after diff block) in the Activity tab.
    /// </summary>
    static class HistoryChangeUIHelper
    {
        const string k_AddedText = "added";
        const string k_ChangedText = "changed";
        const string k_RemovedText = "removed";
        const string k_PreviousLabel = "Previous";
        const string k_NewLabel = "New";

        /// <summary>
        /// Appends a single field-change block to <paramref name="container"/> with title row, optional Previous/New sections, and type-appropriate value display.
        /// </summary>
        /// <param name="container">Parent element (e.g. foldout content).</param>
        /// <param name="change">The field change (FieldName, OldValue, NewValue).</param>
        /// <param name="fieldDefs">Optional metadata field definitions to resolve custom metadata type; can be null.</param>
        public static void Build(VisualElement container, HistoryFieldChange change, IReadOnlyList<IMetadataFieldDefinition> fieldDefs)
        {
            if (change == null)
                return;

            var fieldName = change.FieldName ?? string.Empty;
            var oldVal = change.OldValue;
            var newVal = change.NewValue;

            string actionText;
            string actionClass;
            if (oldVal == null)
            {
                actionText = k_AddedText;
                actionClass = UssStyle.HistoryChangeActionAdded;
            }
            else if (newVal == null)
            {
                actionText = k_RemovedText;
                actionClass = UssStyle.HistoryChangeActionRemoved;
            }
            else
            {
                actionText = k_ChangedText;
                actionClass = UssStyle.HistoryChangeActionChanged;
            }

            Action<VisualElement, string> displayValue = ResolveDisplay(fieldName, fieldDefs);
            BuildChangeBlock(container, fieldName, actionText, actionClass, oldVal, newVal, displayValue);
        }

        /// <summary>
        /// Builds the common layout: title row (field name + action), optional Previous section, optional arrow, optional New section.
        /// </summary>
        public static void BuildChangeBlock(VisualElement container, string fieldName, string actionText, string actionCssClass,
            string oldValue, string newValue, Action<VisualElement, string> displayValue)
        {
            if (displayValue == null)
                displayValue = DisplayText;

            // Check if this is tags field (which needs vertical layout)
            bool isTagsField = fieldName == HistoryDiffHelper.FieldTags;

            var block = new VisualElement();
            block.AddToClassList(UssStyle.HistoryChangeBlock);

            var titleRow = new VisualElement();
            titleRow.AddToClassList(UssStyle.HistoryChangeTitleRow);

            // Add action icon
            var actionIcon = CreateActionIcon(actionText, actionCssClass);
            titleRow.Add(actionIcon);

            var nameLabel = new Label(fieldName);
            nameLabel.AddToClassList(UssStyle.HistoryChangeFieldName);
            titleRow.Add(nameLabel);

            var actionLabel = new Label(actionText);
            actionLabel.AddToClassList(actionCssClass);
            titleRow.Add(actionLabel);

            block.Add(titleRow);

            if (isTagsField)
            {
                // Vertical layout for tags
                if (oldValue != null)
                {
                    var prevLabel = new Label(k_PreviousLabel);
                    prevLabel.AddToClassList(UssStyle.HistoryChangeSectionLabel);
                    block.Add(prevLabel);

                    var prevValueCell = new VisualElement();
                    prevValueCell.AddToClassList(UssStyle.HistoryChangeValueCell);
                    displayValue(prevValueCell, oldValue);
                    block.Add(prevValueCell);
                }

                if (oldValue != null && newValue != null)
                {
                    var arrowContainer = new VisualElement();
                    arrowContainer.AddToClassList(UssStyle.HistoryChangeArrowRow);

                    var arrow = new VisualElement();
                    arrow.AddToClassList(UssStyle.HistoryChangeArrowVertical);
                    arrowContainer.Add(arrow);
                    block.Add(arrowContainer);
                }

                if (newValue != null)
                {
                    var newLabel = new Label(k_NewLabel);
                    newLabel.AddToClassList(UssStyle.HistoryChangeSectionLabel);
                    block.Add(newLabel);

                    var newValueCell = new VisualElement();
                    newValueCell.AddToClassList(UssStyle.HistoryChangeValueCell);
                    newValueCell.AddToClassList(UssStyle.HistoryChangeValueCellNew);
                    displayValue(newValueCell, newValue);
                    block.Add(newValueCell);
                }
            }
            else
            {
                // Horizontal layout for all other fields
                var contentRow = new VisualElement();
                contentRow.style.flexDirection = FlexDirection.Row;
                contentRow.style.alignItems = Align.FlexStart;
                contentRow.style.marginTop = 4;
                contentRow.style.flexGrow = 1;

                if (oldValue != null)
                {
                    var prevColumn = new VisualElement();
                    prevColumn.style.flexDirection = FlexDirection.Column;
                    prevColumn.style.flexGrow = 1;
                    prevColumn.style.flexShrink = 1;
                    prevColumn.style.flexBasis = 0;
                    prevColumn.style.minWidth = 0;

                    var prevLabel = new Label(k_PreviousLabel);
                    prevLabel.AddToClassList(UssStyle.HistoryChangeSectionLabelHorizontal);
                    prevColumn.Add(prevLabel);

                    var prevValueCell = new VisualElement();
                    prevValueCell.AddToClassList(UssStyle.HistoryChangeValueCell);
                    displayValue(prevValueCell, oldValue);
                    prevColumn.Add(prevValueCell);

                    contentRow.Add(prevColumn);
                }

                if (oldValue != null && newValue != null)
                {
                    var arrow = new VisualElement();
                    arrow.AddToClassList(UssStyle.HistoryChangeArrow);
                    arrow.style.marginLeft = 10;
                    arrow.style.marginRight = 10;
                    arrow.style.marginTop = 12;
                    arrow.style.flexShrink = 0;
                    contentRow.Add(arrow);
                }

                if (newValue != null)
                {
                    var newColumn = new VisualElement();
                    newColumn.style.flexDirection = FlexDirection.Column;
                    newColumn.style.flexGrow = 1;
                    newColumn.style.flexShrink = 1;
                    newColumn.style.flexBasis = 0;
                    newColumn.style.minWidth = 0;

                    var newLabel = new Label(k_NewLabel);
                    newLabel.AddToClassList(UssStyle.HistoryChangeSectionLabelHorizontal);
                    newColumn.Add(newLabel);

                    var newValueCell = new VisualElement();
                    newValueCell.AddToClassList(UssStyle.HistoryChangeValueCell);
                    newValueCell.AddToClassList(UssStyle.HistoryChangeValueCellNew);
                    displayValue(newValueCell, newValue);
                    newColumn.Add(newValueCell);

                    contentRow.Add(newColumn);
                }

                block.Add(contentRow);
            }

            container.Add(block);
        }

        /// <summary>
        /// Creates a circular badge icon for the action type (Added, Changed, Removed).
        /// Follows the InitialsIconHelper pattern.
        /// </summary>
        static VisualElement CreateActionIcon(string actionText, string actionClass)
        {
            var icon = new VisualElement();
            icon.name = "ActionIcon";
            icon.AddToClassList(UssStyle.HistoryChangeActionIcon);
            icon.AddToClassList(actionClass);

            var label = new Label(GetActionLetter(actionText));
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.color = Color.white;
            icon.Add(label);

            return icon;
        }

        internal static string GetActionLetter(string actionText)
        {
            return actionText switch
            {
                k_AddedText => "A",
                k_ChangedText => "C",
                k_RemovedText => "R",
                _ => "?"
            };
        }

        static Action<VisualElement, string> ResolveDisplay(string fieldName, IReadOnlyList<IMetadataFieldDefinition> fieldDefs)
        {
            if (fieldName == HistoryDiffHelper.FieldTags)
                return DisplayTags;

            var def = fieldDefs?.FirstOrDefault(d => d != null && string.Equals(d.Key, fieldName, StringComparison.Ordinal));
            if (def != null)
            {
                switch (def.Type)
                {
                    case MetadataFieldType.Boolean:
                        return DisplayBool;
                    case MetadataFieldType.User:
                        return DisplayUser;
                    case MetadataFieldType.SingleSelection:
                    case MetadataFieldType.MultiSelection:
                        return DisplayChips;
                    case MetadataFieldType.Text:
                    case MetadataFieldType.Number:
                    case MetadataFieldType.Timestamp:
                    case MetadataFieldType.Url:
                    default:
                        return DisplayText;
                }
            }

            return DisplayText;
        }

        static void DisplayText(VisualElement container, string value)
        {
            if (container == null) return;
            var label = new Label(value ?? string.Empty);
            label.AddToClassList(UssStyle.DetailsPageEntryValue);
            container.Add(label);
        }

        static void DisplayTags(VisualElement container, string commaValue)
        {
            if (container == null) return;
            var chipContainer = new VisualElement();
            chipContainer.AddToClassList(UssStyle.DetailsPageChipContainer);
            var tags = SplitByComma(commaValue);
            foreach (var tag in tags)
            {
                var chip = new TagChip(tag);
                chip.TagChipPointerUpAction += tagText =>
                {
                    var words = tagText.Split(' ').Where(w => !string.IsNullOrEmpty(w));
                    var pageManager = ServicesContainer.instance.Resolve<IPageManager>();
                    pageManager.PageFilterStrategy.AddSearchFilter(words);
                };
                chipContainer.Add(chip);
            }
            container.Add(chipContainer);
        }

        static void DisplayBool(VisualElement container, string value)
        {
            if (container == null) return;
            if (!bool.TryParse(value, out var flag))
            {
                DisplayText(container, value ?? string.Empty);
                return;
            }
            var toggle = new Toggle { value = flag };
            toggle.SetEnabled(false);
            container.Add(toggle);
        }

        static void DisplayUser(VisualElement container, string userId)
        {
            if (container == null || string.IsNullOrEmpty(userId)) return;
            AssetInspectorUIElementHelper.AddUser(container, null, userId, typeof(UpdatedByFilter));
        }

        static void DisplayChips(VisualElement container, string commaValue)
        {
            if (container == null) return;
            var chipContainer = new VisualElement();
            chipContainer.AddToClassList(UssStyle.DetailsPageChipContainer);
            var values = SplitByComma(commaValue);
            foreach (var v in values)
            {
                chipContainer.Add(new Chip(v, isSelectable: false));
            }
            container.Add(chipContainer);
        }

        internal static IEnumerable<string> SplitByComma(string commaValue)
        {
            if (string.IsNullOrEmpty(commaValue))
                return Array.Empty<string>();
            // Split on comma (with or without spaces) and trim each value
            return commaValue.Split(',')
                .Select(s => s?.Trim())
                .Where(s => !string.IsNullOrEmpty(s));
        }
    }
}
