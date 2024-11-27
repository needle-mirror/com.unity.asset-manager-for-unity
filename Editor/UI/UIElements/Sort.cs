using System;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine.UIElements;
namespace Unity.AssetManager.UI.Editor
{
    class Sort:GridTool
    {
        const string k_UssClassName = "unity-sort";
        const string k_DropdownUssClassName = k_UssClassName + "-dropdown";
        const string k_OrderUssClassName = k_UssClassName + "-order";
        const string k_AscendingUssClassName = k_OrderUssClassName + "--ascending";
        const string k_DescendingUssClassName = k_OrderUssClassName + "--descending";

        readonly DropdownField m_DropdownField;

        public Sort(IPageManager pageManager, IProjectOrganizationProvider projectOrganizationProvider)
            : base(pageManager, projectOrganizationProvider)
        {
            AddToClassList(k_UssClassName);

            m_DropdownField = new DropdownField(L10n.Tr(Constants.Sort));
            if (m_PageManager.ActivePage != null)
            {
                SetupSortField(m_PageManager.ActivePage);
            }
            m_DropdownField.AddToClassList(k_DropdownUssClassName);

            m_DropdownField.RegisterValueChangedCallback(ev =>
            {
                OnValueChanged(m_PageManager.ActivePage.SortOptions[ev.newValue], m_PageManager.SortingOrder);
            });
            Add(m_DropdownField);

            var orderButton = new Button();
            orderButton.AddToClassList(k_OrderUssClassName);
            orderButton.AddToClassList(m_PageManager.SortingOrder == SortingOrder.Ascending ? k_AscendingUssClassName : k_DescendingUssClassName);
            orderButton.clicked += () =>
            {
                var sortingOrder = m_PageManager.SortingOrder == SortingOrder.Ascending ? SortingOrder.Descending : SortingOrder.Ascending;
                orderButton.RemoveFromClassList(sortingOrder == SortingOrder.Ascending ? k_DescendingUssClassName : k_AscendingUssClassName);
                orderButton.AddToClassList(sortingOrder == SortingOrder.Ascending ? k_AscendingUssClassName : k_DescendingUssClassName);
                OnValueChanged(m_PageManager.ActivePage.SortOptions[m_DropdownField.value], sortingOrder);
            };
            Add(orderButton);
        }

        void OnValueChanged(SortField sortField, SortingOrder sortingOrder)
        {
            m_PageManager.SetSortValues(sortField, sortingOrder);

            AnalyticsSender.SendEvent(new SortEvent(sortField.ToString(), sortingOrder == SortingOrder.Ascending));
        }

        protected override void OnActivePageChanged(IPage page)
        {
            SetupSortField(page);
        }

        protected override bool IsDisplayed(IPage page)
        {
            if (page is BasePage basePage)
            {
                return basePage.DisplaySort;
            }

            return base.IsDisplayed(page);
        }

        void SetupSortField(IPage page)
        {
            m_DropdownField.choices = page.SortOptions.Keys.ToList();
            var index = (int)m_PageManager.SortField;
            if (index >= m_DropdownField.choices.Count)
            {
                index = 0;
            }
            m_DropdownField.index = index;
        }
    }
}
