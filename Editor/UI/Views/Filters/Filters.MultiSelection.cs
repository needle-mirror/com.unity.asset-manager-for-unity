using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    partial class Filters
    {
        async Task AddMultiSelectionItems(Button chip, BaseFilter filter, CancellationToken cancellationToken)
        {
            var selections = await filter.GetSelections(true);
            if (selections == null)
                return;

            if (cancellationToken.IsCancellationRequested)
                return;

            m_PopupManager.Clear();

            if (selections.Any() || filter.AllowCustomChoices)
            {
                var scrollView = new ScrollView();
                m_PopupManager.Container.Add(scrollView);

                var selectedFilters = filter.SelectedFilters?.ToList() ?? new List<string>();
                var checkmarks = new List<Image>();

                Button clearButton = null;
                Button applyButton = null;
                (clearButton, applyButton) = AddButtons(chip, filter, () => selectedFilters,
                    () => {
                        selectedFilters.Clear();
                        foreach (var checkmark in checkmarks)
                        {
                            checkmark.visible = false;
                        }

                        clearButton?.SetEnabled(false);
                        applyButton?.SetEnabled(false);
                    });
                var hasSelections = selectedFilters.Any();
                clearButton.SetEnabled(hasSelections);
                applyButton.SetEnabled(hasSelections);

                foreach (var selection in selections)
                {
                    var filterSelection = new VisualElement();
                    filterSelection.AddToClassList(UssStyle.k_FilterItemSelection);
                    filterSelection.style.paddingLeft = 0;

                    var checkbox = new VisualElement();
                    checkbox.AddToClassList(UssStyle.k_FilterItemSelectionCheckbox);
                    filterSelection.Add(checkbox);

                    var checkmark = new Image();
                    checkmark.AddToClassList(UssStyle.k_FilterItemSelectionCheckmark);
                    checkmarks.Add(checkmark);
                    filterSelection.Add(checkmark);

                    if (selection.Icon != null)
                    {
                        selection.Icon.AddToClassList(UssStyle.k_FilterItemSelectionIcon);
                        filterSelection.Add(selection.Icon);
                    }

                    var label = new TextElement();
                    label.text = selection.Text;
                    label.tooltip = selection.Tooltip;
                    filterSelection.Add(label);

                    checkmark.visible = filter.SelectedFilters?.Any(s => s == selection.Text) ?? false;

                    filterSelection.RegisterCallback<ClickEvent>(evt =>
                    {
                        evt.StopPropagation();

                        if (selectedFilters.Contains(selection.Text))
                        {
                            selectedFilters.Remove(selection.Text);
                            var anySelected = selectedFilters.Any();
                            clearButton.SetEnabled(anySelected);
                            applyButton.SetEnabled(anySelected);
                        }
                        else
                        {
                            selectedFilters.Add(selection.Text);
                            clearButton.SetEnabled(true);
                            applyButton.SetEnabled(true);
                        }

                        checkmark.visible = selectedFilters.Contains(selection.Text);
                    });

                    scrollView.Add(filterSelection);
                }

                if (filter.AllowCustomChoices)
                {
                    AddCustomChoiceRow(scrollView, selectedFilters, checkmarks, clearButton, applyButton);
                }
            }
            else
            {
                var noSelection = new TextElement();
                noSelection.AddToClassList(UssStyle.k_FilterItemNoSelection);
                noSelection.text = L10n.Tr(Constants.NoSelectionsText);
                m_PopupManager.Container.Add(noSelection);
            }
        }

        void AddCustomChoiceRow(ScrollView scrollView, List<string> selectedFilters, List<Image> checkmarks,
            Button clearButton, Button applyButton)
        {
            // Add separator line before the add row
            var separator = new VisualElement();
            separator.AddToClassList(UssStyle.k_FilterSeparatorLine);
            scrollView.Add(separator);

            // Container for both states
            var container = new VisualElement();

            // "Add value" row (default state)
            var addValueRow = new VisualElement();
            addValueRow.AddToClassList(UssStyle.k_FilterAddChoiceRow);

            var plusLabel = new Label("+");
            plusLabel.AddToClassList(UssStyle.k_FilterAddChoiceButton);
            addValueRow.Add(plusLabel);

            var addValueLabel = new Label(L10n.Tr(Constants.AddValueLabel));
            addValueLabel.AddToClassList(UssStyle.k_FilterAddChoiceLabel);
            addValueRow.Add(addValueLabel);

            container.Add(addValueRow);

            // Input row (hidden initially)
            var inputRow = new VisualElement();
            inputRow.AddToClassList(UssStyle.k_FilterAddChoiceInputRow);
            inputRow.style.display = DisplayStyle.None;

            var textField = new TextField();
            textField.AddToClassList(UssStyle.k_FilterAddChoiceTextField);
            inputRow.Add(textField);

            var addButton = new Button();
            addButton.text = L10n.Tr(Constants.Add);
            addButton.focusable = false;
            addButton.AddToClassList(UssStyle.k_FilterAddChoiceAddButton);
            inputRow.Add(addButton);

            container.Add(inputRow);

            // Click handler to show input row
            addValueRow.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                addValueRow.style.display = DisplayStyle.None;
                inputRow.style.display = DisplayStyle.Flex;
                textField.Focus();
            });

            addButton.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                AddCustomChoice(scrollView, separator, textField, selectedFilters, checkmarks,
                    clearButton, applyButton, inputRow, addValueRow);
            });

            textField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    evt.StopPropagation();
                    AddCustomChoice(scrollView, separator, textField, selectedFilters, checkmarks,
                        clearButton, applyButton, inputRow, addValueRow);
                }
            });

            scrollView.Add(container);
        }

        void AddCustomChoice(ScrollView scrollView, VisualElement separator, TextField textField,
            List<string> selectedFilters, List<Image> checkmarks, Button clearButton, Button applyButton,
            VisualElement inputRow, VisualElement addValueRow)
        {
            var choiceText = textField.value?.Trim();
            if (string.IsNullOrEmpty(choiceText))
                return;

            if (!selectedFilters.Contains(choiceText))
            {
                selectedFilters.Add(choiceText);
                clearButton.SetEnabled(true);
                applyButton.SetEnabled(true);

                var filterSelection = CreateCustomChoiceSelectionRow(choiceText, selectedFilters, checkmarks,
                    clearButton, applyButton);

                // Insert before the separator
                var separatorIndex = scrollView.IndexOf(separator);
                scrollView.Insert(separatorIndex, filterSelection);
            }

            textField.value = string.Empty;

            // Switch back to add value row
            inputRow.style.display = DisplayStyle.None;
            addValueRow.style.display = DisplayStyle.Flex;
        }

        VisualElement CreateCustomChoiceSelectionRow(string choiceText, List<string> selectedFilters,
            List<Image> checkmarks, Button clearButton, Button applyButton)
        {
            var filterSelection = new VisualElement();
            filterSelection.AddToClassList(UssStyle.k_FilterItemSelection);
            filterSelection.style.paddingLeft = 0;

            var checkbox = new VisualElement();
            checkbox.AddToClassList(UssStyle.k_FilterItemSelectionCheckbox);
            filterSelection.Add(checkbox);

            var checkmark = new Image();
            checkmark.AddToClassList(UssStyle.k_FilterItemSelectionCheckmark);
            checkmark.visible = true;
            checkmarks.Add(checkmark);
            filterSelection.Add(checkmark);

            var label = new TextElement();
            label.text = choiceText;
            filterSelection.Add(label);

            filterSelection.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();

                if (selectedFilters.Contains(choiceText))
                {
                    selectedFilters.Remove(choiceText);
                    var anySelected = selectedFilters.Any();
                    clearButton.SetEnabled(anySelected);
                    applyButton.SetEnabled(anySelected);
                }
                else
                {
                    selectedFilters.Add(choiceText);
                    clearButton.SetEnabled(true);
                    applyButton.SetEnabled(true);
                }

                checkmark.visible = selectedFilters.Contains(choiceText);
            });

            return filterSelection;
        }
    }
}
