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
        
        readonly string m_FoldoutExpandedClassName = "details-foldout-expanded";

        public bool Expanded
        {
            get => m_Foldout.value;
            set => m_Foldout.value = value;
        }

        public bool IsEmpty => m_ListView.itemsSource == null || m_ListView.itemsSource.Count == 0;
        protected IList Items => m_ListView.itemsSource;

        protected abstract TBinding MakeItem();
        protected abstract void BindItem(TBinding element, int index);

        protected ItemFoldout(VisualElement parent, string foldoutName, string listViewName, string loadingLabelName, string foldoutTitle = null, string foldoutExpandedClassName = null)
        {
            m_Foldout = parent.Q<Foldout>(foldoutName);
            m_LoadingLabel = parent.Q<Label>(loadingLabelName);
            m_ListView = parent.Q<ListView>(listViewName);
            
            // In case the uxml file was not pre build, we can manually create them
            if (m_Foldout == null)
            {
                m_Foldout = new Foldout();
                m_Foldout.AddToClassList(foldoutName);
                m_Foldout.name = foldoutName;
                if (!string.IsNullOrEmpty(foldoutTitle))
                {
                    m_Foldout.text = foldoutTitle;
                }
                parent.contentContainer.hierarchy.Add(m_Foldout);
                
                m_LoadingLabel = new Label();
                m_LoadingLabel.AddToClassList(loadingLabelName);
                m_LoadingLabel.name = loadingLabelName;
                m_Foldout.contentContainer.hierarchy.Add(m_LoadingLabel);
                
                m_ListView = new ListView();
                m_ListView.AddToClassList(listViewName);
                m_ListView.name = listViewName;
                m_Foldout.contentContainer.hierarchy.Add(m_ListView);
            }

            if (foldoutExpandedClassName != null)
            {
                m_FoldoutExpandedClassName = foldoutExpandedClassName;
            }

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

        protected virtual IList PrepareListItem(IAssetData assetData, IEnumerable<TData> items)
        {
            return items.ToList();
        }

        public virtual void Clear()
        {
            m_ListView.itemsSource = null;
        }

        public void Populate(IAssetData assetData, IEnumerable<TData> items)
        {
            m_ListView.itemsSource = PrepareListItem(assetData, items);
            m_ListView.makeItem = MakeItem;
            m_ListView.bindItem = (element, i) => { BindItem((TBinding)element, i); };
            m_ListView.fixedItemHeight = 30;
        }
    }
}
