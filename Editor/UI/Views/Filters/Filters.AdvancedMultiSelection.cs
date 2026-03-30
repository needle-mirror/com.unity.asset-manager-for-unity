using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    partial class Filters
    {
        const int k_MaxAdvancedSelectionRows = 2;

        async Task AddAdvancedMultiSelection(Button chip, BaseFilter filter, CancellationToken cancellationToken)
        {
            var selections = await filter.GetSelections(true);
            if (selections == null || cancellationToken.IsCancellationRequested)
                return;

            m_PopupManager.Clear();

            var rowsContainer = new VisualElement();
            m_PopupManager.Container.Add(rowsContainer);

            // Build choices list from selections, filtering out empty/whitespace values
            var allChoices = selections.Select(s => s.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            if (allChoices.Count == 0)
            {
                // Display text saying there are no available options in this project
                var noOptionsLabel = new Label(L10n.Tr(Constants.NoAvailableOptions));
                rowsContainer.Add(noOptionsLabel);

                return;
            }

            var rows = new List<(DropdownField modeDropdown, List<string> selectedValues, VisualElement pillsContainer, DropdownField valueDropdown, VisualElement rowContainer)>();

            Button addValueButton = null;

            string GetOppositeMode(string mode)
            {
                return mode == Constants.Is ? Constants.IsNot : Constants.Is;
            }

            void UpdateButtonStates(Button clearBtn, Button applyBtn)
            {
                var hasAnyValue = rows.Any(r => r.selectedValues.Count > 0);
                clearBtn.SetEnabled(hasAnyValue);
                applyBtn.SetEnabled(hasAnyValue);
            }

            void UpdateAddButtonState()
            {
                addValueButton.SetEnabled(rows.Count < k_MaxAdvancedSelectionRows);
            }

            void UpdateModeDropdownStates()
            {
                // When there are 2 rows, the second dropdown is disabled and set to opposite of first
                if (rows.Count == 2)
                {
                    rows[1].modeDropdown.SetEnabled(false);
                    rows[1].modeDropdown.SetValueWithoutNotify(GetOppositeMode(rows[0].modeDropdown.value));
                }
                else if (rows.Count == 1)
                {
                    rows[0].modeDropdown.SetEnabled(true);
                }
            }

            Button applyButton = null;

            void UpdateAvailableChoicesForRow(int rowIndex)
            {
                if (rowIndex < 0 || rowIndex >= rows.Count) return;
                var row = rows[rowIndex];
                var availableChoices = allChoices.Where(c =>
                    !rows.Any(r => r.selectedValues.Any(sv => string.Equals(sv, c, System.StringComparison.OrdinalIgnoreCase)))
                ).ToList();
                row.valueDropdown.choices = availableChoices;
                row.valueDropdown.SetValueWithoutNotify(null);
                var shouldHide = availableChoices.Count == 0;
                if (shouldHide)
                {
                    applyButton?.Focus(); // Focus the apply button to prevent the popup from closing
                }
                UIElementsUtils.SetDisplay(row.valueDropdown, !shouldHide);
            }

            void UpdateAvailableChoices(int rowIndex)
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    UpdateAvailableChoicesForRow(i);
                }
            }

            void AddPill(int rowIndex, string value, Button clearBtn, Button applyBtn)
            {
                if (rowIndex < 0 || rowIndex >= rows.Count) return;
                var row = rows[rowIndex];

                if (row.selectedValues.Any(sv => string.Equals(sv, value, System.StringComparison.OrdinalIgnoreCase))) return;

                row.selectedValues.Add(value);

                var pill = new VisualElement();
                pill.AddToClassList(UssStyle.k_FilterAdvancedPill);

                var label = new Label(value);
                label.AddToClassList(UssStyle.k_FilterAdvancedPillLabel);
                pill.Add(label);

                var removeBtn = new Image();
                removeBtn.AddToClassList(UssStyle.k_FilterItemChipDelete);
                removeBtn.AddToClassList(UssStyle.k_FilterAdvancedPillRemove);
                removeBtn.AddManipulator(new Clickable(() =>
                {
                    row.selectedValues.Remove(value);
                    pill.RemoveFromHierarchy();
                    UpdateButtonStates(clearBtn, applyBtn);
                    UpdateAvailableChoices(rowIndex);
                }));
                pill.Add(removeBtn);

                row.pillsContainer.Add(pill);
                UpdateButtonStates(clearBtn, applyBtn);
                UpdateAvailableChoices(rowIndex);
            }

            VisualElement CreateRow(string mode, List<string> initialValues, Button clearBtn, Button applyBtn, bool canRemove)
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
                modeDropdown.choices = new List<string> { Constants.Is, Constants.IsNot };
                modeDropdown.value = mode == Constants.IsNot ? Constants.IsNot : Constants.Is;
                modeDropdown.AddToClassList(UssStyle.k_FilterAdvancedModeDropdown);
                modeDropdownRow.Add(modeDropdown);

                var selectedValues = new List<string>();
                var rowTuple = (modeDropdown, selectedValues, (VisualElement)null, (DropdownField)null, rowContainer);

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
                        UpdateModeDropdownStates();
                        UpdateAddButtonState();
                    }));
                    modeDropdownRow.Add(removeButton);
                }

                // Value selection row with pills container and dropdown
                var valueFieldRow = new VisualElement();
                valueFieldRow.AddToClassList(UssStyle.k_FilterExpandedInputRow);
                inputContainer.Add(valueFieldRow);

                // Container styled like a text box
                var textBoxContainer = new VisualElement();
                textBoxContainer.AddToClassList(UssStyle.k_FilterAdvancedTextBox);
                valueFieldRow.Add(textBoxContainer);

                // Pills container (inside the text box) - stacked vertically
                var pillsContainer = new VisualElement();
                pillsContainer.AddToClassList(UssStyle.k_FilterAdvancedPillsContainer);
                textBoxContainer.Add(pillsContainer);

                // Dropdown for adding values (shows as arrow button)
                var valueDropdown = new DropdownField();
                valueDropdown.choices = allChoices.ToList();
                valueDropdown.AddToClassList(UssStyle.k_FilterAdvancedValueDropdown);
                textBoxContainer.Add(valueDropdown);

                rowTuple = (modeDropdown, selectedValues, pillsContainer, valueDropdown, rowContainer);
                rows.Add(rowTuple);

                var rowIndex = rows.Count - 1;

                // When a value is selected from dropdown, add it as a pill
                valueDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (!string.IsNullOrEmpty(evt.newValue))
                    {
                        AddPill(rowIndex, evt.newValue, clearBtn, applyBtn);
                    }
                });

                // When first row's mode changes, update second row's mode to opposite
                modeDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (rows.Count == 2 && rows[0].modeDropdown == modeDropdown)
                    {
                        rows[1].modeDropdown.SetValueWithoutNotify(GetOppositeMode(evt.newValue));
                    }
                });

                // Add initial values as pills
                foreach (var value in initialValues)
                {
                    AddPill(rowIndex, value, clearBtn, applyBtn);
                }

                UpdateAvailableChoices(rowIndex);

                return rowContainer;
            }

            List<string> CollectValues()
            {
                // Format: [included1, included2, ..., "", excluded1, excluded2, ...]
                // Empty string separates included from excluded values
                var included = new List<string>();
                var excluded = new List<string>();

                foreach (var row in rows)
                {
                    if (row.modeDropdown.value == Constants.Is)
                    {
                        included.AddRange(row.selectedValues);
                    }
                    else if (row.modeDropdown.value == Constants.IsNot)
                    {
                        excluded.AddRange(row.selectedValues);
                    }
                }

                var values = new List<string>();
                values.AddRange(included);
                values.Add(string.Empty); // Separator
                values.AddRange(excluded);
                return values;
            }

            Button clearButton;
            (clearButton, applyButton) = AddButtons(chip, filter, CollectValues,
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
            addValueButton = new Button();
            addValueButton.text = L10n.Tr(Constants.AddValue);
            addValueButton.AddToClassList(UssStyle.k_FilterSelectionButton);
            addValueButton.RegisterCallback<ClickEvent>(evt =>
            {
                if (rows.Count >= k_MaxAdvancedSelectionRows) return;

                // New row gets the opposite mode of the first row
                var initialMode = rows.Count > 0 ? GetOppositeMode(rows[0].modeDropdown.value) : Constants.Is;
                var newRow = CreateRow(initialMode, new List<string>(), clearButton, applyButton, true);
                rowsContainer.Add(newRow);
                UpdateButtonStates(clearButton, applyButton);
                UpdateModeDropdownStates();
                evt.StopImmediatePropagation();
                applyButton.Focus(); // Focus the apply button to prevent the popup from closing
                UpdateAddButtonState();
            });
            buttonContainer.Add(addValueButton);

            // Right side container for "Clear" and "Apply"
            var rightContainer = new VisualElement();
            rightContainer.AddToClassList(UssStyle.k_FilterExpandedRightContainer);
            buttonContainer.Add(rightContainer);

            rightContainer.Add(clearButton);
            rightContainer.Add(applyButton);

            // Parse existing selected filters
            // Format: [included1, included2, ..., "", excluded1, excluded2, ...]
            // Empty string separates included from excluded values
            var selectedFilters = filter.SelectedFilters?.ToList() ?? new List<string>();
            if (selectedFilters.Count >= 1)
            {
                var isValues = new List<string>();
                var isNotValues = new List<string>();
                var isExcluded = false;

                foreach (var value in selectedFilters)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        isExcluded = true;
                        continue;
                    }

                    if (isExcluded)
                        isNotValues.Add(value);
                    else
                        isValues.Add(value);
                }

                // Create rows for each mode that has values
                var isFirstRow = true;
                if (isValues.Count > 0)
                {
                    var row = CreateRow(Constants.Is, isValues, clearButton, applyButton, false);
                    rowsContainer.Add(row);
                    isFirstRow = false;
                }

                if (isNotValues.Count > 0)
                {
                    var row = CreateRow(Constants.IsNot, isNotValues, clearButton, applyButton, !isFirstRow);
                    rowsContainer.Add(row);
                    isFirstRow = false;
                }

                // If no values were parsed, add an empty row
                if (isFirstRow)
                {
                    var row = CreateRow(Constants.Is, new List<string>(), clearButton, applyButton, false);
                    rowsContainer.Add(row);
                }
            }
            else
            {
                // Add initial empty row (cannot be removed)
                var row = CreateRow(Constants.Is, new List<string>(), clearButton, applyButton, false);
                rowsContainer.Add(row);
            }

            UpdateButtonStates(clearButton, applyButton);
            UpdateModeDropdownStates();
            UpdateAddButtonState();
        }
    }
}
