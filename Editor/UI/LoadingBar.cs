using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class LoadingBar: VisualElement
    {
        private const string k_LoadingBarUssClassName = "loading-bar";
        private const string k_LoadingBarLabelUssClassName = "loading-bar-label";
        private const string k_LoadingBarHighPositionUssClassName = "loading-bar--high-position";

        readonly LoadingIcon m_LoadingIcon;
        readonly Label m_Label;

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
            if (page is { isLoading: true })
            {
                m_Label.AddToClassList(k_LoadingBarLabelUssClassName);

                if (page.assetList?.Count == 0)
                {
                    AddToClassList(k_LoadingBarHighPositionUssClassName);
                }
                else
                {
                    RemoveFromClassList(k_LoadingBarHighPositionUssClassName);
                }
                
                m_LoadingIcon.PlayAnimation();
                UIElementsUtils.Show(this);
            }
            else
            {
                RemoveFromClassList(k_LoadingBarHighPositionUssClassName);
                if (!showMessageWhenNoMoreItem || page == null || page.hasMoreItems)
                {
                    m_LoadingIcon.StopAnimation();
                    UIElementsUtils.Hide(this);
                }
            }
        }
    }
}
