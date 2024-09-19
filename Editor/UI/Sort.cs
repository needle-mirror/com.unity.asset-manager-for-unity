using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;
namespace Unity.AssetManager.Editor
{
    class Sort:GridTool
    {
        const string k_UssClassName = "unity-sort";
        const string k_DropdownUssClassName = k_UssClassName + "-dropdown";
        const string k_OrderUssClassName = k_UssClassName + "-order";
        const string k_AscendingUssClassName = k_OrderUssClassName + "--ascending";
        const string k_DescendingUssClassName = k_OrderUssClassName + "--descending";

        static readonly Dictionary<string, SortField> k_SortOptions = new ()
        {
            {"Name", SortField.Name},
            {"Last Modified", SortField.Updated},
            {"Upload Date", SortField.Created},
            {"Description", SortField.Description},
            {"Asset Type", SortField.PrimaryType},
            {"Status", SortField.Status}
        };

        public Sort(IPageManager pageManager, IProjectOrganizationProvider projectOrganizationProvider)
            : base(pageManager, projectOrganizationProvider)
        {
            AddToClassList(k_UssClassName);

            var dropDown = new DropdownField(L10n.Tr(Constants.Sort))
            {
                choices = k_SortOptions.Keys.ToList(),
                index = (int)m_PageManager.SortField
            };
            dropDown.AddToClassList(k_DropdownUssClassName);

            dropDown.RegisterValueChangedCallback(ev =>
            {
                OnValueChanged(k_SortOptions[ev.newValue], m_PageManager.SortingOrder);
            });
            Add(dropDown);

            var orderButton = new Button();
            orderButton.AddToClassList(k_OrderUssClassName);
            orderButton.AddToClassList(m_PageManager.SortingOrder == SortingOrder.Ascending ? k_AscendingUssClassName : k_DescendingUssClassName);
            orderButton.clicked += () =>
            {
                var sortingOrder = m_PageManager.SortingOrder == SortingOrder.Ascending ? SortingOrder.Descending : SortingOrder.Ascending;
                orderButton.RemoveFromClassList(sortingOrder == SortingOrder.Ascending ? k_DescendingUssClassName : k_AscendingUssClassName);
                orderButton.AddToClassList(sortingOrder == SortingOrder.Ascending ? k_AscendingUssClassName : k_DescendingUssClassName);
                OnValueChanged(k_SortOptions[dropDown.value], sortingOrder);
            };
            Add(orderButton);
        }

        void OnValueChanged(SortField sortField, SortingOrder sortingOrder)
        {
            m_PageManager.SetSortValues(sortField, sortingOrder);
        }
    }
}
