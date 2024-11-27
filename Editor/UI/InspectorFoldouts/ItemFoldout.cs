using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    abstract class ItemFoldout<TData, TBinding> where TBinding : VisualElement // TODO Convert to a VisualElement
    {
        readonly ListView m_ListView;
        readonly Foldout m_Foldout;

        readonly string m_FoldoutExpandedClassName = "details-foldout-expanded";
        readonly string m_FoldoutTitle;

        public bool Expanded
        {
            get => m_Foldout.value;
            set => m_Foldout.value = value;
        }

        public bool IsEmpty => m_ListView.itemsSource == null || m_ListView.itemsSource.Count == 0;
        protected IList Items => m_ListView.itemsSource;

        protected abstract TBinding MakeItem();
        protected abstract void BindItem(TBinding element, int index);

        protected event Action<IEnumerable<object>> SelectionChanged;

        protected ItemFoldout(VisualElement parent, string foldoutName, string listViewName, string foldoutTitle = null,
            string foldoutExpandedClassName = null)
        {
            m_Foldout = parent.Q<Foldout>(foldoutName);
            m_ListView = parent.Q<ListView>(listViewName);

            // In case the uxml file was not pre build, we can manually create them
            if (m_Foldout == null)
            {
                m_Foldout = new Foldout();
                m_Foldout.AddToClassList(foldoutName);
                m_Foldout.name = foldoutName;
                if (!string.IsNullOrEmpty(foldoutTitle))
                {
                    m_FoldoutTitle = foldoutTitle;
                    m_Foldout.text = L10n.Tr(m_FoldoutTitle);
                }

                parent.contentContainer.hierarchy.Add(m_Foldout);

                m_ListView = new ListView();
                m_ListView.AddToClassList(listViewName);
                m_ListView.name = listViewName;
                m_Foldout.contentContainer.hierarchy.Add(m_ListView);
            }
            else
            {
                if (!string.IsNullOrEmpty(foldoutTitle))
                {
                    m_FoldoutTitle = foldoutTitle;
                    m_Foldout.text = L10n.Tr(m_FoldoutTitle);
                }
                else
                {
                    m_FoldoutTitle = m_Foldout.text;
                }
            }

            var toggle = m_Foldout.Q<Toggle>();
            if (toggle != null)
            {
                toggle.focusable = false;
            }
            m_ListView.focusable = false;

            if (foldoutExpandedClassName != null)
            {
                m_FoldoutExpandedClassName = foldoutExpandedClassName;
            }

            m_Foldout.viewDataKey = foldoutName;
            m_ListView.viewDataKey = listViewName;
            m_ListView.selectionType = SelectionType.None;
            m_ListView.selectionChanged += RaiseSelectionChangedEvent;
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
                m_Foldout.AddToClassList(m_FoldoutExpandedClassName);
            }
            else
            {
                m_Foldout.RemoveFromClassList(m_FoldoutExpandedClassName);
            }
        }

        public void StartPopulating()
        {
            Clear();
            UIElementsUtils.Hide(m_Foldout);
        }

        public void StopPopulating()
        {
            var hasItems = !IsEmpty;
            UIElementsUtils.SetDisplay(m_Foldout, hasItems);
            UIElementsUtils.SetDisplay(m_ListView, hasItems);
        }

        protected virtual IList PrepareListItem(BaseAssetData assetData, IEnumerable<TData> items)
        {
            return items.ToList();
        }

        public virtual void Clear()
        {
            m_Foldout.text = L10n.Tr(m_FoldoutTitle);
            m_ListView.itemsSource = null;
        }

        public virtual void RemoveItems(IEnumerable<TData> items)
        {
            var itemsToRemove = items.ToList();
            var itemsSource = m_ListView.itemsSource as List<TData>;

            if (itemsSource == null)
                return;

            foreach (var item in itemsToRemove)
            {
                itemsSource.Remove(item);
            }

            m_ListView.itemsSource = itemsSource;
            m_Foldout.text = $"{L10n.Tr(m_FoldoutTitle)} ({m_ListView.itemsSource.Count})";
            m_ListView.RefreshItems();
            StopPopulating();
        }

        public void Populate(BaseAssetData assetData, IEnumerable<TData> items)
        {
            m_ListView.itemsSource = PrepareListItem(assetData, items);
            m_ListView.makeItem = MakeItem;
            m_ListView.bindItem = (element, i) => { BindItem((TBinding)element, i); };
            m_ListView.fixedItemHeight = 30;

            m_Foldout.text = $"{L10n.Tr(m_FoldoutTitle)} ({m_ListView.itemsSource.Count})";
        }

        void RaiseSelectionChangedEvent(IEnumerable<object> items)
        {
            SelectionChanged?.Invoke(items);
        }
    }
}
