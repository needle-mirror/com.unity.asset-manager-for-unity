using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    partial class Filters
    {
        void AddDateSelection(Button chip, BaseFilter filter)
        {
            m_PopupManager.Clear();

            var currentMode = Constants.Is;
            var currentDate = DateTime.Today;

            if (filter.SelectedFilters != null && filter.SelectedFilters.Count >= 2)
            {
                currentMode = filter.SelectedFilters[0];
                try
                {
                    currentDate = DateTime.Parse(filter.SelectedFilters[1], DateTimeFormatInfo.CurrentInfo, DateTimeStyles.RoundtripKind);
                }
                catch
                {
                    currentDate = DateTime.Today;
                }
            }

            var mainContainer = new VisualElement();
            mainContainer.AddToClassList(UssStyle.k_FilterSelectionContainer);
            mainContainer.AddToClassList(UssStyle.k_FilterExpandedInputColumn);
            m_PopupManager.Container.Add(mainContainer);

            var labelsRow = new VisualElement();
            labelsRow.AddToClassList(UssStyle.FlexRowStyleClass);
            mainContainer.Add(labelsRow);

            var matchTypeLabel = new Label(L10n.Tr("Match type"));
            matchTypeLabel.AddToClassList(UssStyle.k_FilterSelectionLabel);
            matchTypeLabel.AddToClassList(UssStyle.k_FilterDateSelectionLabel);
            labelsRow.Add(matchTypeLabel);

            var dateLabel = new Label(L10n.Tr("Select date"));
            dateLabel.AddToClassList(UssStyle.k_FilterSelectionLabel);
            dateLabel.AddToClassList(UssStyle.k_FilterDateSelectionDateContainer);
            labelsRow.Add(dateLabel);

            var inputsRow = new VisualElement();
            inputsRow.AddToClassList(UssStyle.FlexRowStyleClass);
            mainContainer.Add(inputsRow);

            var modeDropdown = new DropdownField();
            modeDropdown.choices = new List<string> { Constants.Is, Constants.IsNot };
            modeDropdown.value = currentMode == Constants.IsNot ? Constants.IsNot : Constants.Is;
            modeDropdown.AddToClassList(UssStyle.k_FilterDateSelectionModeDropdown);
            inputsRow.Add(modeDropdown);

            var yearField = new IntegerField
            {
                value = currentDate.Year
            };
            yearField.AddToClassList(UssStyle.TimestampPickerFieldStyleClass);
            yearField.AddToClassList(UssStyle.k_FilterDateSelectionDateContainer);
            inputsRow.Add(yearField);

            inputsRow.Add(new Label("/"));

            var monthDropdown = new DropdownField
            {
                choices = GenerateNumberRange(1, 12),
                index = currentDate.Month - 1
            };
            monthDropdown.AddToClassList(UssStyle.TimestampPickerFieldStyleClass);
            inputsRow.Add(monthDropdown);

            inputsRow.Add(new Label("/"));

            var dayDropdown = new DropdownField();
            UpdateDayChoices(dayDropdown, yearField.value, currentDate.Month);
            dayDropdown.index = currentDate.Day - 1;
            dayDropdown.AddToClassList(UssStyle.TimestampPickerFieldStyleClass);
            inputsRow.Add(dayDropdown);

            void UpdateDayDropdown()
            {
                if (int.TryParse(monthDropdown.value, out int month))
                {
                    UpdateDayChoices(dayDropdown, yearField.value, month);
                }
            }

            yearField.RegisterValueChangedCallback(evt =>
            {
                var clampedYear = Math.Clamp(evt.newValue, 1, 9999);
                if (clampedYear != evt.newValue)
                {
                    yearField.SetValueWithoutNotify(clampedYear);
                }
                UpdateDayDropdown();
            });
            monthDropdown.RegisterValueChangedCallback(evt => UpdateDayDropdown());

            List<string> CollectValues()
            {
                if (int.TryParse(monthDropdown.value, out int month) &&
                    int.TryParse(dayDropdown.value, out int day) &&
                    yearField.value > 0 && yearField.value < 10000)
                {
                    var daysInMonth = DateTime.DaysInMonth(yearField.value, month);
                    var clampedDay = Math.Min(day, daysInMonth);
                    var date = new DateTime(yearField.value, month, clampedDay);
                    return new List<string> { modeDropdown.value, date.ToString("o") };
                }
                return null;
            }

            var (clearButton, applyButton) = AddButtons(chip, filter, CollectValues,
                () =>
                {
                    modeDropdown.value = Constants.Is;
                    yearField.value = DateTime.Today.Year;
                    monthDropdown.index = DateTime.Today.Month - 1;
                    UpdateDayDropdown();
                    dayDropdown.index = DateTime.Today.Day - 1;
                });

            var hasValue = filter.SelectedFilters != null && filter.SelectedFilters.Count >= 2;
            clearButton.SetEnabled(hasValue);
            applyButton.SetEnabled(true);
        }

        static void UpdateDayChoices(DropdownField dayDropdown, int year, int month)
        {
            var daysInMonth = DateTime.DaysInMonth(year > 0 && year < 10000 ? year : DateTime.Today.Year, month);
            var oldIndex = dayDropdown.index;
            dayDropdown.choices = GenerateNumberRange(1, daysInMonth);

            if (oldIndex >= daysInMonth)
            {
                dayDropdown.index = daysInMonth - 1;
            }
            else if (oldIndex >= 0)
            {
                dayDropdown.index = oldIndex;
            }
            else
            {
                dayDropdown.index = 0;
            }
        }

        static List<string> GenerateNumberRange(int start, int end)
        {
            var range = new List<string>();
            for (int i = start; i <= end; i++)
            {
                range.Add(i.ToString("D2"));
            }
            return range;
        }
    }
}
