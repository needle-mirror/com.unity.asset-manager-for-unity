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
        async Task AddSingleSelectionItems(Button chip, BaseFilter filter, CancellationToken cancellationToken)
        {
            var selections = await filter.GetSelections(true);
            if (selections == null)
                return;

            if (cancellationToken.IsCancellationRequested)
                return;

            m_PopupManager.Clear();

            if (selections.Any())
            {
                var scrollView = new ScrollView();
                m_PopupManager.Container.Add(scrollView);

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

                        chip.AddToClassList(UssStyle.k_FilterItemChipSet);
                        m_PopupManager.Hide();

                        ApplyFilter(filter, new List<string> {selection.Text});
                    });

                    scrollView.Add(filterSelection);
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
    }
}
