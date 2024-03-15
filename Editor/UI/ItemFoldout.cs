using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    abstract class ItemFoldout<TData, TBinding> where TBinding : VisualElement // TODO Convert to a VisualElement
    {
        readonly Label m_LoadingLabel;
        readonly ListView m_ListView;
        readonly Foldout m_Foldout;

        public bool Expanded
        {
            get => m_Foldout.value;
            set => m_Foldout.value = value;
        }

        public bool IsEmpty => m_ListView.itemsSource == null || m_ListView.itemsSource.Count == 0;
        protected IList Items => m_ListView.itemsSource;

        protected abstract TBinding MakeItem();
        protected abstract void BindItem(TBinding element, int index);

        protected ItemFoldout(VisualElement parent, string foldoutName, string listViewName, string loadingLabelName)
        {
            m_LoadingLabel = parent.Q<Label>(loadingLabelName);
            m_ListView = parent.Q<ListView>(listViewName);
            m_Foldout = parent.Q<Foldout>(foldoutName);

            m_LoadingLabel.text = L10n.Tr("Loading...");
            UIElementsUtils.Hide(m_LoadingLabel);

            m_Foldout.viewDataKey = foldoutName;
            m_ListView.viewDataKey = listViewName;
        }

        public void RegisterValueChangedCallback(Action<object> action)
        {
            m_Foldout.RegisterValueChangedCallback(_ =>
            {
                RefreshFoldoutStyleBasedOnExpansionStatus();
                action?.Invoke(null);
            });
        }

        public void RefreshFoldoutStyleBasedOnExpansionStatus()
        {
            if (m_Foldout.value)
            {
                m_Foldout.AddToClassList("details-foldout-expanded");
            }
            else
            {
                m_Foldout.RemoveFromClassList("details-foldout-expanded");
            }
        }

        public void StartPopulating()
        {
            Clear();
            UIElementsUtils.Show(m_LoadingLabel);
            UIElementsUtils.Show(m_Foldout);
        }

        public void StopPopulating()
        {
            UIElementsUtils.Hide(m_LoadingLabel);

            var hasItems = !IsEmpty;
            UIElementsUtils.SetDisplay(m_Foldout, hasItems);
            UIElementsUtils.SetDisplay(m_ListView, hasItems);
        }

        protected virtual IList PrepareListItem(IEnumerable<TData> items)
        {
            return items.ToList();
        }

        public virtual void Clear()
        {
            m_ListView.itemsSource = null;
        }

        public void Populate(IEnumerable<TData> items)
        {
            m_ListView.itemsSource = PrepareListItem(items);
            m_ListView.makeItem = MakeItem;
            m_ListView.bindItem = (element, i) => { BindItem((TBinding)element, i); };
            m_ListView.fixedItemHeight = 30;
        }
    }
}