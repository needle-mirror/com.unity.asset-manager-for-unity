using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    partial class Filters
    {
        void AddTextSelection(Button chip, BaseFilter filter)
        {
            m_PopupManager.Clear();

            var container = new VisualElement();
            container.AddToClassList(UssStyle.k_FilterSelectionContainer);
            m_PopupManager.Container.Add(container);

            if (filter is AssetIdFilter)
            {
                var label = new Label(L10n.Tr(Constants.EnterAssetIdText));
                label.AddToClassList(UssStyle.k_FilterSelectionLabel);
                container.Add(label);
            }

            var textField = new TextField();
            textField.value = filter.SelectedFilters?.FirstOrDefault() ?? string.Empty;
            textField.AddToClassList(UssStyle.k_FilterSelectionNumberField);
            container.Add(textField);

            var (clearButton, applyButton) = AddButtons(chip, filter, () => new List<string>{textField.value},
                () =>
                {
                    m_PopupManager.Hide();
                    ApplyFilter(filter, null);
                });

            var hasText = !string.IsNullOrEmpty(textField.value);
            clearButton.SetEnabled(hasText);
            applyButton.SetEnabled(hasText);

            textField.RegisterValueChangedCallback(evt =>
            {
                var hasValue = !string.IsNullOrEmpty(evt.newValue);
                clearButton.SetEnabled(hasValue);
                applyButton.SetEnabled(hasValue);
            });
        }

        void AddUrlSelection(Button chip, BaseFilter filter)
        {
            m_PopupManager.Clear();

            var container = new VisualElement();
            container.AddToClassList(UssStyle.k_FilterSelectionContainer);
            m_PopupManager.Container.Add(container);

            var label = new Label(L10n.Tr(Constants.EnterUrlText));
            label.AddToClassList(UssStyle.k_FilterSelectionLabel);
            container.Add(label);

            var textField = new TextField();
            textField.value = filter.SelectedFilters?.FirstOrDefault() ?? string.Empty;
            textField.AddToClassList(UssStyle.k_FilterSelectionNumberField);
            container.Add(textField);

            var (clearButton, applyButton) = AddButtons(chip, filter, () => new List<string>{textField.value},
                () =>
                {
                    m_PopupManager.Hide();
                    ApplyFilter(filter, null);
                });

            var hasUrl = !string.IsNullOrEmpty(textField.value);
            clearButton.SetEnabled(hasUrl);
            applyButton.SetEnabled(hasUrl);
        }
    }
}
