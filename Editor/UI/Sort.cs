using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;
namespace Unity.AssetManager.Editor
{
    enum SortingOrder
    {
        Ascending,
        Descending
    }

    enum SortField
    {
        Name,
        Updated,
        Created,
        Description,
        PrimaryType,
        Status
    }

    class Sort:VisualElement
    {
        const string k_UssClassName = "unity-sort";
        const string k_DropdownUssClassName = k_UssClassName + "-dropdown";
        const string k_OrderUssClassName = k_UssClassName + "-order";
        const string k_AscendingUssClassName = k_OrderUssClassName + "--ascending";
        const string k_DescendingUssClassName = k_OrderUssClassName + "--descending";
        const string k_SortFieldPrefKey = "com.unity.asset-manager-for-unity.sortField";
        const string k_SortingOrderKey = "com.unity.asset-manager-for-unity.sortingOrder";

        static readonly Dictionary<string, SortField> k_SortOptions = new ()
        {
            {"Name", SortField.Name},
            {"Last Modified", SortField.Updated},
            {"Upload Date", SortField.Created},
            {"Description", SortField.Description},
            {"Asset Type", SortField.PrimaryType},
            {"Status", SortField.Status}
        };

        SortField m_SortField;
        SortingOrder m_SortingOrder;

        public event Action<SortField, SortingOrder> ValueChanged;

        public Sort()
        {
            m_SortField = (SortField)EditorPrefs.GetInt(k_SortFieldPrefKey, (int)SortField.Name);
            m_SortingOrder = (SortingOrder)EditorPrefs.GetInt(k_SortingOrderKey, (int)SortingOrder.Ascending);

            AddToClassList(k_UssClassName);

            var dropDown = new DropdownField(L10n.Tr(Constants.Sort))
            {
                choices = k_SortOptions.Keys.ToList(),
                index = (int)m_SortField
            };
            dropDown.AddToClassList(k_DropdownUssClassName);

            dropDown.RegisterValueChangedCallback(ev =>
            {
                OnValueChanged(k_SortOptions[ev.newValue], m_SortingOrder);
            });
            Add(dropDown);

            var orderButton = new Button();
            orderButton.AddToClassList(k_OrderUssClassName);
            orderButton.AddToClassList(m_SortingOrder == SortingOrder.Ascending ? k_AscendingUssClassName : k_DescendingUssClassName);
            orderButton.clicked += () =>
            {
                var sortingOrder = m_SortingOrder == SortingOrder.Ascending ? SortingOrder.Descending : SortingOrder.Ascending;
                orderButton.RemoveFromClassList(sortingOrder == SortingOrder.Ascending ? k_DescendingUssClassName : k_AscendingUssClassName);
                orderButton.AddToClassList(sortingOrder == SortingOrder.Ascending ? k_AscendingUssClassName : k_DescendingUssClassName);
                OnValueChanged(k_SortOptions[dropDown.value], sortingOrder);
            };
            Add(orderButton);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            ValueChanged?.Invoke(m_SortField, m_SortingOrder);
        }

        void OnValueChanged(SortField sortField, SortingOrder sortingOrder)
        {
            if(sortField == m_SortField && sortingOrder == m_SortingOrder)
                return;

            m_SortField = sortField;
            m_SortingOrder = sortingOrder;
            EditorPrefs.SetInt(k_SortFieldPrefKey, (int)m_SortField);
            EditorPrefs.SetInt(k_SortingOrderKey, (int)m_SortingOrder);
            ValueChanged?.Invoke(m_SortField, m_SortingOrder);
        }
    }
}
