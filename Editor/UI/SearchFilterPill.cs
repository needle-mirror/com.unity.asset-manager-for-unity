using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class SearchFilterPill : VisualElement
    {
        const string k_UssName = "SearchFilterPill";
        const string k_CancelButtonUnityUssStyleClass = "unity-search-field-base__cancel-button";
        const string k_SearchPillIMGUIStyleName = "CN CountBadge";

        const int k_PillSizeDelta = -1;

        IMGUIContainer m_SearchPillContainer;

        string m_SearchFilter;

        internal string SearchFilter => m_SearchFilter;

        Action<string> m_OnDismiss;

        public new class UxmlFactory : UxmlFactory<SearchFilterPill> { }

        public SearchFilterPill()
        {

        }

        internal SearchFilterPill(string searchFilter, Action<string> onDismiss)
        {
            Initialize(searchFilter, onDismiss);
        }

        internal void Initialize(string searchFilter, Action<string> onDismiss)
        {
            AddToClassList(k_UssName);

            m_SearchFilter = searchFilter;
            m_OnDismiss = onDismiss;

            style.flexDirection = FlexDirection.Row;
            style.height = EditorGUIUtility.singleLineHeight + k_PillSizeDelta;

            m_SearchPillContainer = new IMGUIContainer(OnGUIHandler);
            m_SearchPillContainer.style.color = new StyleColor(Color.blue);
            m_SearchPillContainer.style.flexDirection = FlexDirection.Row;

            var searchFilterLabel = new Label(m_SearchFilter);
            var clearButton = new Button(() => onDismiss.Invoke(m_SearchFilter));
            clearButton.AddToClassList(k_CancelButtonUnityUssStyleClass);

            Add(searchFilterLabel);
            Add(clearButton);
        }

        ~SearchFilterPill()
        {
            m_SearchPillContainer.Dispose();
        }

        internal void DismissSearchPill()
        {
            m_OnDismiss?.Invoke(m_SearchFilter);
        }

        void OnGUIHandler()
        {
            GUILayout.Box("", k_SearchPillIMGUIStyleName);
        }
    }
}
