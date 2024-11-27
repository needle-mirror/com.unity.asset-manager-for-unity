using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    static partial class UssStyle
    {
        public const string InvalidFieldLabelStyleClass = "invalid-field-label";
        public const string InvalidTextFieldStyleClass = "invalid-text-field";
        public const string UnityTextElementStyleClass = "unity-text-element";
        public const string TimestampPickerFieldStyleClass = "timestamp-picker-field";
        public const string FlexRowStyleClass = "flex-row";
        public const string FlexColumnStyleClass = "flex-column";
    }

   class TimestampPicker : VisualElement
   {
        readonly DoubleField m_YearPicker;
        readonly DropdownField m_MonthPicker;
        readonly DropdownField m_DayPicker;
        readonly Label m_DateLabel;
        readonly DropdownField m_HourPicker;
        readonly DropdownField m_MinutePicker;
        readonly DropdownField m_MeridiemPicker;

        public event Action<DateTime> ValueChanged;

        protected TimestampPicker()
        {
            var now = DateTime.Now;

            m_YearPicker = new DoubleField
            {
                value = now.Year
            };
            m_YearPicker.AddToClassList(UssStyle.TimestampPickerFieldStyleClass);
            m_YearPicker.RegisterValueChangedCallback(evt =>
            {
                if (TryUpdateTimestamp())
                {
                    UpdateDayPicker();
                }
            });

            m_MonthPicker = new DropdownField
            {
                choices = GenerateNumberRange(1, 12),
                index = now.Month - 1
            };
            m_MonthPicker.AddToClassList(UssStyle.TimestampPickerFieldStyleClass);
            m_MonthPicker.RegisterValueChangedCallback(evt =>
            {
                if (TryUpdateTimestamp())
                {
                    UpdateDayPicker();
                }
            });

            m_DayPicker = new DropdownField();
            UpdateDayPicker();
            m_DayPicker.index = now.Day - 1;
            m_DayPicker.AddToClassList(UssStyle.TimestampPickerFieldStyleClass);
            m_DayPicker.RegisterValueChangedCallback(evt => TryUpdateTimestamp());

            var dateHorizontalContainer = new VisualElement();
            dateHorizontalContainer.AddToClassList(UssStyle.FlexRowStyleClass);
            dateHorizontalContainer.Add(m_YearPicker);
            dateHorizontalContainer.Add(new Label("/"));
            dateHorizontalContainer.Add(m_MonthPicker);
            dateHorizontalContainer.Add(new Label("/"));
            dateHorizontalContainer.Add(m_DayPicker);

            m_DateLabel = new Label(Constants.DateLabel);

            m_HourPicker = new DropdownField
            {
                choices = GenerateNumberRange(1, 12),
                index = now.Hour == 12 ? 11 : now.Hour % 12 - 1
            };
            m_HourPicker.AddToClassList(UssStyle.TimestampPickerFieldStyleClass);
            m_HourPicker.RegisterValueChangedCallback(evt => TryUpdateTimestamp());

            m_MinutePicker = new DropdownField
            {
                choices = GenerateNumberRange(0, 60),
                index = now.Minute - 1
            };
            m_MinutePicker.AddToClassList(UssStyle.TimestampPickerFieldStyleClass);
            m_MinutePicker.RegisterValueChangedCallback(evt => TryUpdateTimestamp());

            m_MeridiemPicker = new DropdownField
            {
                choices = new List<string> { "AM", "PM" },
                index = now.Hour < 12 ? 0 : 1
            };
            m_MeridiemPicker.AddToClassList(UssStyle.TimestampPickerFieldStyleClass);
            m_MeridiemPicker.RegisterValueChangedCallback(evt => TryUpdateTimestamp());

            var timeHorizontalContainer = new VisualElement();
            timeHorizontalContainer.AddToClassList(UssStyle.FlexRowStyleClass);
            timeHorizontalContainer.Add(m_HourPicker);
            timeHorizontalContainer.Add(new Label(":"));
            timeHorizontalContainer.Add(m_MinutePicker);
            timeHorizontalContainer.Add(m_MeridiemPicker);

            var timeLabel = new Label(Constants.TimeLabel);

            var verticalContainer = new VisualElement();
            verticalContainer.AddToClassList(UssStyle.FlexColumnStyleClass);
            verticalContainer.Add(dateHorizontalContainer);
            verticalContainer.Add(m_DateLabel);
            verticalContainer.Add(timeHorizontalContainer);
            verticalContainer.Add(timeLabel);

            Add(verticalContainer);
        }

        protected void SetTimeUsingMultipleTimestamps(List<DateTime> timestamps)
        {
            var firstTimestamp = timestamps.FirstOrDefault();

            if (timestamps.TrueForAll(x => x.Month == firstTimestamp.Month))
            {
                m_MonthPicker.index = firstTimestamp.Month - 1;
            }
            else
            {
                m_MonthPicker.showMixedValue = true;
            }

            if (timestamps.TrueForAll(x => x.Day == firstTimestamp.Day))
            {
                m_DayPicker.index = firstTimestamp.Day - 1;
            }
            else
            {
                m_DayPicker.showMixedValue = true;
            }

            if (timestamps.TrueForAll(x => x.Year == firstTimestamp.Year))
            {
                m_YearPicker.value = firstTimestamp.Year;
            }
            else
            {
                m_YearPicker.showMixedValue = true;
            }

            if (timestamps.TrueForAll(x => Utilities.ConvertTo12HourTime(x.Hour) ==
                                           Utilities.ConvertTo12HourTime(firstTimestamp.Hour)))
            {
                m_HourPicker.index = Utilities.ConvertTo12HourTime(firstTimestamp.Hour) - 1;
            }
            else
            {
                m_HourPicker.showMixedValue = true;
            }

            if (timestamps.TrueForAll(x => x.Minute == firstTimestamp.Minute))
            {
                m_MinutePicker.index = firstTimestamp.Minute;
            }
            else
            {
                m_MinutePicker.showMixedValue = true;
            }

            if (timestamps.TrueForAll(x => x.Hour < 12))
            {
                m_MeridiemPicker.index = 0;
            }
            else if (timestamps.TrueForAll(x => x.Hour >= 12))
            {
                m_MeridiemPicker.index = 1;
            }
            else
            {
                m_MeridiemPicker.showMixedValue = true;
            }
        }

        bool TryUpdateTimestamp()
        {
            if (int.TryParse(m_MonthPicker.value, out int month)
                && int.TryParse(m_DayPicker.value, out int day)
                && int.TryParse(m_HourPicker.value, out int hour12)
                && int.TryParse(m_MinutePicker.value, out int minute)
                && !string.IsNullOrEmpty(m_MeridiemPicker.value))
            {
                if (m_YearPicker.value is > 0 and < 10000)
                {
                    var dateTime = new DateTime((int)m_YearPicker.value, month, day,
                        Utilities.ConvertTo24HourTime(hour12, m_MeridiemPicker.value == "PM"), minute,
                        0, DateTimeKind.Utc);
                    ValueChanged?.Invoke(dateTime);

                    m_DateLabel.RemoveFromClassList(UssStyle.InvalidFieldLabelStyleClass);
                    m_DateLabel.AddToClassList(UssStyle.UnityTextElementStyleClass);
                    m_DateLabel.text = Constants.DateLabel;

                    m_YearPicker.RemoveFromClassList(UssStyle.InvalidTextFieldStyleClass);

                    return true;
                }

                m_DateLabel.AddToClassList(UssStyle.InvalidFieldLabelStyleClass);
                m_DateLabel.RemoveFromClassList(UssStyle.UnityTextElementStyleClass);
                m_DateLabel.text = Constants.InvalidYearLabel;

                m_YearPicker.AddToClassList(UssStyle.InvalidTextFieldStyleClass);

                return false;
            }

            throw new FormatException(Constants.UnexpectedTimestampFormat);
        }

        void UpdateDayPicker()
        {
            if (int.TryParse(m_MonthPicker.value, out int month))
            {
                int daysInMonth = DateTime.DaysInMonth((int)m_YearPicker.value, month);
                m_DayPicker.choices = GenerateNumberRange(1, daysInMonth);

                if (m_DayPicker.index >= daysInMonth)
                {
                    m_DayPicker.index = daysInMonth - 1;
                }
            }
        }

        List<string> GenerateNumberRange(int start, int end)
        {
            List<string> range = new List<string>();

            for (int i = start; i <= end; i++)
            {
                range.Add(i.ToString());
            }

            return range;
        }
   }
}
