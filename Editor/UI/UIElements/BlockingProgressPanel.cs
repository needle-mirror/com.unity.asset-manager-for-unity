using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    static partial class UssStyle
    {
        public static readonly string BlockingProgressPanel = "blocking-progress-panel-container";
    }

    class BlockingProgressPanel : VisualElement
    {
        readonly ProgressBar m_ProgressBar;
        readonly Label m_MessageLabel;

        public BlockingProgressPanel()
        {
            var container = new VisualElement();
            container.AddToClassList(UssStyle.BlockingProgressPanel);
            Add(container);

            m_ProgressBar = new ProgressBar();
            container.Add(m_ProgressBar);

            m_MessageLabel = new Label();
            UIElementsUtils.Hide(m_MessageLabel);
            container.Add(m_MessageLabel);
        }

        public void SetMessage(string message)
        {
            UIElementsUtils.SetDisplay(m_MessageLabel, !string.IsNullOrWhiteSpace(message));
            m_MessageLabel.text = message;
            m_MessageLabel.tooltip = message;
        }

        public void SetProgress(float progress)
        {
            m_ProgressBar.SetProgress(progress);
        }
    }
}
