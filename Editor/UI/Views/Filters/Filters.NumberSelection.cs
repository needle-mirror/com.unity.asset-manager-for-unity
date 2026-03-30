using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    partial class Filters
    {
        void AddNumberSelection(Button chip, BaseFilter filter)
        {
            m_PopupManager.Clear();

            var numberFilter = (NumberMetadataFilter)filter;
            if(numberFilter == null)
                return;

            var valueField = new DoubleField(L10n.Tr(Constants.EnterNumberText))
            {
                value = numberFilter.Value
            };

            valueField.AddToClassList(UssStyle.k_FilterSelectionNumberField);
            valueField.isDelayed = true;
            m_PopupManager.Container.Add(valueField);

            var (clearButton, applyButton) = AddButtons(chip, filter, () => new List<string>{valueField.value.ToString()},
                () =>
                {
                    valueField.value = 0;
                });

            valueField.RegisterValueChangedCallback(evt =>
            {
                var hasValue = evt.newValue != 0;
                clearButton.SetEnabled(hasValue);
                applyButton.SetEnabled(hasValue);
            });

            var hasValue = numberFilter.Value != 0;
            clearButton.SetEnabled(hasValue);
            applyButton.SetEnabled(hasValue);
        }

        void AddRangeNumberSelection(Button chip, BaseFilter filter)
        {
            m_PopupManager.Clear();

            var numberFilter = (NumberRangeMetadataFilter)filter;
            if(numberFilter == null)
                return;

            var fromField = new DoubleField(L10n.Tr(Constants.FromText))
            {
                value = numberFilter.FromValue
            };

            var toField = new DoubleField(L10n.Tr(Constants.ToText))
            {
                value = numberFilter.ToValue
            };

            fromField.AddToClassList(UssStyle.k_FilterSelectionNumberField);
            fromField.isDelayed = true;
            fromField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue > toField.value)
                {
                    toField.value = evt.newValue;
                }
            });
            m_PopupManager.Container.Add(fromField);

            toField.AddToClassList(UssStyle.k_FilterSelectionNumberField);
            toField.isDelayed = true;
            toField.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue < fromField.value)
                {
                    toField.value = evt.previousValue;
                }
            });
            m_PopupManager.Container.Add(toField);

            var (clearButton, applyButton) = AddButtons(chip, filter, () => new List<string>{fromField.value.ToString(), toField.value.ToString()},
                () =>
                {
                    fromField.value = 0;
                    toField.value = 0;
                });

            var hasValues = numberFilter.FromValue != 0 || numberFilter.ToValue != 0;
            clearButton.SetEnabled(hasValues);
            applyButton.SetEnabled(hasValues);
        }
    }
}
