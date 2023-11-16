using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class LoadingBar: VisualElement
    {
        private const string k_LoadingBarUssClassName = "loading-bar";
        private const string k_LoadingBarLabelUssClassName = "loading-bar-label";
        private const string k_LoadingBarNoMorAssetsLabelUssClassName = "loading-bar-no-more-assets-label";

        LoadingIcon m_LoadingIcon;
        Label m_Label;

        public LoadingBar()
        {
            pickingMode = PickingMode.Ignore;

            m_LoadingIcon = new LoadingIcon();
            m_Label = new Label(L10n.Tr("Loading Assets"));

            AddToClassList(k_LoadingBarUssClassName);

            Add(m_LoadingIcon);
            Add(m_Label);
        }

        public void Refresh(IPage page, bool showMessageWhenNoMoreItem = false)
        {
            if (page != null && page.isLoading)
            {
                m_Label.text = L10n.Tr("Loading Assets");
                m_Label.RemoveFromClassList(k_LoadingBarNoMorAssetsLabelUssClassName);
                m_Label.AddToClassList(k_LoadingBarLabelUssClassName);
                m_LoadingIcon.visible = true;
                UIElementsUtils.Show(this);
            }
            else
            {
                if (!showMessageWhenNoMoreItem || page == null || page.hasMoreItems)
                {
                    UIElementsUtils.Hide(this);
                    return;
                }
                ShowNoMoreAssetsLoadingBar();
            }
        }
        
        void ShowNoMoreAssetsLoadingBar()
        {
            m_Label.text = L10n.Tr("No More Assets");
            m_Label.RemoveFromClassList(k_LoadingBarLabelUssClassName);
            m_Label.AddToClassList(k_LoadingBarNoMorAssetsLabelUssClassName);
            m_LoadingIcon.visible = false;
            
            UIElementsUtils.Show(this);
            schedule.Execute(() =>
            {
                UIElementsUtils.Hide(this);
            }).StartingIn(3000);
        }
    }
}
