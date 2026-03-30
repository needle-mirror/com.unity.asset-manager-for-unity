using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    partial class Filters
    {
        void AddRangeTimestampSelection(Button chip, BaseFilter filter)
        {
            m_PopupManager.Clear();

            var timestampFilter = (TimestampMetadataFilter)filter;

            if(timestampFilter == null)
                return;

            var fromField = new TimestampPicker(timestampFilter.FromValue != DateTime.MinValue ? timestampFilter.FromValue : DateTime.Today , false);
            var toField = new TimestampPicker(timestampFilter.ToValue != DateTime.MinValue ? timestampFilter.ToValue : EndOfDay(DateTime.Today), false);

            fromField.ValueChanged += dateTime =>
            {
                if(DateTime.Compare(dateTime, toField.Timestamp) > 0)
                {
                    toField.Timestamp = EndOfDay(dateTime);
                }
            };

            toField.ValueChanged += dateTime =>
            {
                if (DateTime.Compare(dateTime, fromField.Timestamp) < 0)
                {
                    toField.Timestamp = EndOfDay(fromField.Timestamp);
                }
            };

            var fromContainer = new VisualElement();
            fromContainer.AddToClassList(UssStyle.k_FilterSelectionContainer);
            m_PopupManager.Container.Add(fromContainer);

            var fromLabel = new Label(L10n.Tr(Constants.FromText));
            fromLabel.AddToClassList(UssStyle.k_FilterSelectionLabel);
            fromContainer.Add(fromLabel);
            fromContainer.Add(fromField);

            var toContainer = new VisualElement();
            toContainer.AddToClassList(UssStyle.k_FilterSelectionContainer);
            m_PopupManager.Container.Add(toContainer);

            var toLabel = new Label(L10n.Tr(Constants.ToText));
            toLabel.AddToClassList(UssStyle.k_FilterSelectionLabel);
            toContainer.Add(toLabel);
            toContainer.Add(toField);

            var (clearButton, applyButton) = AddButtons(chip, filter, () => new List<string>{fromField.Timestamp.ToString(), toField.Timestamp.ToString()},
                () =>
                {
                    fromField.Timestamp = DateTime.Today;
                    toField.Timestamp = EndOfDay(DateTime.Today);
                });

            var hasTimestamps = timestampFilter.FromValue != DateTime.MinValue || timestampFilter.ToValue != DateTime.MinValue;
            clearButton.SetEnabled(hasTimestamps);
            applyButton.SetEnabled(hasTimestamps);
        }

        static DateTime EndOfDay(DateTime date)
        {
            return new DateTime(date.Year, date.Month, date.Day, 23, 59, 59, DateTimeKind.Local);
        }
    }
}
