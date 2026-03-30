using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    partial class Filters
    {
        void AddMultiTextSelection(Button chip, BaseFilter filter)
        {
            m_PopupManager.Clear();

            var rowsContainer = new VisualElement();
            m_PopupManager.Container.Add(rowsContainer);

            var rows = new List<(DropdownField dropdown, TextField textField, VisualElement rowContainer)>();
            var separators = new List<Label>();

            // Logic dropdown (will be placed after first row)
            var logicDropdown = new DropdownField();
            logicDropdown.choices = new List<string> { Constants.And, Constants.Or };
            logicDropdown.value = Constants.And;

            // Logic selector container: "Apply [dropdown] between values -----"
            var logicSelectorContainer = new VisualElement();
            logicSelectorContainer.AddToClassList(UssStyle.k_FilterExpandedLogicSelector);

            var applyLabel = new Label(L10n.Tr(Constants.ApplyLogic));
            applyLabel.AddToClassList(UssStyle.k_FilterExpandedLogicLabel);
            logicSelectorContainer.Add(applyLabel);

            logicSelectorContainer.Add(logicDropdown);

            var betweenLabel = new Label(L10n.Tr(Constants.BetweenValues));
            betweenLabel.AddToClassList(UssStyle.k_FilterExpandedLogicLabel);
            logicSelectorContainer.Add(betweenLabel);

            var logicLine = new VisualElement();
            logicLine.AddToClassList(UssStyle.k_FilterSeparatorLine);
            logicLine.AddToClassList(UssStyle.k_FilterExpandedLogicLine);
            logicSelectorContainer.Add(logicLine);

            void UpdateSeparatorLabels()
            {
                var labelText = logicDropdown.value;
                foreach (var separator in separators)
                {
                    separator.text = L10n.Tr(labelText);
                }
            }

            void UpdateModeDropdownStates()
            {
                var isOrMode = logicDropdown.value == Constants.Or;
                for (var i = 0; i < rows.Count; i++)
                {
                    // In OR mode, only the first dropdown is enabled
                    rows[i].dropdown.SetEnabled(!isOrMode || i == 0);
                }
            }

            void SyncAllModeDropdowns(string mode)
            {
                foreach (var row in rows)
                {
                    row.dropdown.SetValueWithoutNotify(mode);
                }
            }

            void UpdateButtonStates(Button clearBtn, Button applyBtn)
            {
                var hasAnyText = rows.Any(r => !string.IsNullOrEmpty(r.textField.value));
                clearBtn.SetEnabled(hasAnyText);
                applyBtn.SetEnabled(hasAnyText);
            }

            void UpdateLogicSelectorVisibility()
            {
                // Only show the logic selector if there's more than one row
                UIElementsUtils.SetDisplay(logicSelectorContainer, rows.Count > 1);
            }

            VisualElement CreateRow(string mode, string text, Button clearBtn, Button applyBtn, bool canRemove)
            {
                var rowContainer = new VisualElement();

                var inputContainer = new VisualElement();
                inputContainer.AddToClassList(UssStyle.k_FilterSelectionContainer);
                inputContainer.AddToClassList(UssStyle.k_FilterExpandedInputColumn);
                rowContainer.Add(inputContainer);

                // Mode dropdown row (with optional remove button)
                var modeDropdownRow = new VisualElement();
                modeDropdownRow.AddToClassList(UssStyle.k_FilterExpandedModeRow);
                inputContainer.Add(modeDropdownRow);

                var modeDropdown = new DropdownField();
                modeDropdown.choices = new List<string> { Constants.Contains, Constants.DoesNotContain };
                modeDropdown.value = mode == Constants.DoesNotContain ? Constants.DoesNotContain : Constants.Contains;
                modeDropdownRow.Add(modeDropdown);

                var rowTuple = (modeDropdown, (TextField)null, rowContainer);

                if (canRemove)
                {
                    var removeButton = new Image();
                    removeButton.AddToClassList(UssStyle.k_FilterItemChipDelete);
                    removeButton.AddToClassList(UssStyle.k_FilterExpandedRemoveButton);
                    removeButton.AddManipulator(new Clickable(() =>
                    {
                        rows.Remove(rowTuple);
                        rowContainer.RemoveFromHierarchy();
                        UpdateButtonStates(clearBtn, applyBtn);
                        UpdateLogicSelectorVisibility();
                    }));
                    modeDropdownRow.Add(removeButton);
                }

                var textFieldRow = new VisualElement();
                textFieldRow.AddToClassList(UssStyle.k_FilterExpandedInputRow);
                inputContainer.Add(textFieldRow);

                var textField = new TextField();
                textField.value = text;
                textField.AddToClassList(UssStyle.k_FilterSelectionNumberField);
                textField.AddToClassList(UssStyle.k_FilterExpandedTextField);
                textFieldRow.Add(textField);

                rowTuple = (modeDropdown, textField, rowContainer);
                rows.Add(rowTuple);

                // When in OR mode and this is the first dropdown, sync all dropdowns
                modeDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (logicDropdown.value == Constants.Or && rows.Count > 0 && rows[0].dropdown == modeDropdown)
                    {
                        SyncAllModeDropdowns(evt.newValue);
                    }
                });

                textField.RegisterValueChangedCallback(_ => UpdateButtonStates(clearBtn, applyBtn));

                return rowContainer;
            }

            List<string> CollectValues()
            {
                var values = new List<string>();
                // First element is the logic mode
                values.Add(logicDropdown.value);
                foreach (var row in rows)
                {
                    if (!string.IsNullOrEmpty(row.textField.value))
                    {
                        values.Add(row.dropdown.value);
                        values.Add(row.textField.value);
                    }
                }
                return values;
            }

            var (clearButton, applyButton) = AddButtons(chip, filter, CollectValues,
                () =>
                {
                    m_PopupManager.Hide();
                    ApplyFilter(filter, null);
                });

            // Get the button container (last child added by AddButtons)
            var buttonContainer = m_PopupManager.Container.Children().Last();

            // Remove existing buttons to reorganize
            clearButton.RemoveFromHierarchy();
            applyButton.RemoveFromHierarchy();

            // Set up flex layout for left/right alignment
            buttonContainer.AddToClassList(UssStyle.k_FilterExpandedButtonContainer);

            // Left side: "+ Add value" button
            var addValueButton = new Button();
            addValueButton.text = L10n.Tr(Constants.AddValue);
            addValueButton.AddToClassList(UssStyle.k_FilterSelectionButton);
            addValueButton.clicked += () =>
            {
                // In OR mode, new rows inherit the mode from the first row
                var initialMode = logicDropdown.value == Constants.Or && rows.Count > 0
                    ? rows[0].dropdown.value
                    : Constants.Contains;
                var newRow = CreateRow(initialMode, string.Empty, clearButton, applyButton, true);
                rowsContainer.Add(newRow);
                UpdateModeDropdownStates();
                UpdateLogicSelectorVisibility();
            };
            buttonContainer.Add(addValueButton);

            // Right side container for "Clear" and "Apply"
            var rightContainer = new VisualElement();
            rightContainer.AddToClassList(UssStyle.k_FilterExpandedRightContainer);
            buttonContainer.Add(rightContainer);

            rightContainer.Add(clearButton);

            // Add Apply button
            rightContainer.Add(applyButton);

            // Update separator labels and mode dropdown states when logic mode changes
            logicDropdown.RegisterValueChangedCallback(evt =>
            {
                UpdateSeparatorLabels();
                UpdateModeDropdownStates();

                // When switching to OR mode, sync all dropdowns to match the first one
                if (evt.newValue == Constants.Or && rows.Count > 0)
                {
                    SyncAllModeDropdowns(rows[0].dropdown.value);
                }
            });

            // Parse existing selected filters (first element is logic mode, then pairs of mode, value)
            var selectedFilters = filter.SelectedFilters?.ToList() ?? new List<string>();
            if (selectedFilters.Count >= 3)
            {
                // First element is logic mode
                logicDropdown.value = selectedFilters[0] == Constants.Or ? Constants.Or : Constants.And;

                // Remaining elements are pairs of mode, value
                for (var i = 1; i < selectedFilters.Count - 1; i += 2)
                {
                    var mode = selectedFilters[i];
                    var text = selectedFilters[i + 1];
                    // First row cannot be removed
                    var isFirstRow = i == 1;
                    var row = CreateRow(mode, text, clearButton, applyButton, !isFirstRow);
                    rowsContainer.Add(row);

                    // Add logic selector after first row
                    if (isFirstRow)
                    {
                        rowsContainer.Add(logicSelectorContainer);
                    }
                }
            }
            else
            {
                // Add initial empty row (cannot be removed)
                var row = CreateRow(Constants.Contains, string.Empty, clearButton, applyButton, false);
                rowsContainer.Add(row);
                // Add logic selector after first row (hidden initially)
                rowsContainer.Add(logicSelectorContainer);
            }

            UpdateButtonStates(clearButton, applyButton);
            UpdateModeDropdownStates();
            UpdateLogicSelectorVisibility();
        }

        static (VisualElement container, Label label) CreateLogicSeparator(string logicMode)
        {
            var separatorContainer = new VisualElement();
            separatorContainer.AddToClassList(UssStyle.k_FilterAndSeparator);

            var leftLine = new VisualElement();
            leftLine.AddToClassList(UssStyle.k_FilterSeparatorLine);
            leftLine.AddToClassList(UssStyle.k_FilterAndSeparatorLine);
            separatorContainer.Add(leftLine);

            var logicLabel = new Label(L10n.Tr(logicMode));
            logicLabel.AddToClassList(UssStyle.k_FilterAndSeparatorLabel);
            separatorContainer.Add(logicLabel);

            var rightLine = new VisualElement();
            rightLine.AddToClassList(UssStyle.k_FilterSeparatorLine);
            rightLine.AddToClassList(UssStyle.k_FilterAndSeparatorLine);
            separatorContainer.Add(rightLine);

            return (separatorContainer, logicLabel);
        }
    }
}
