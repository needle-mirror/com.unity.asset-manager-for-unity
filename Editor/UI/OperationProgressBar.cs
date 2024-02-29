using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class OperationProgressBar : VisualElement
    {
        const string k_ProgressBarContainerUssClassName = "download-progress-bar-container";
        const string k_ProgressBarBackgroundUssClassName = "download-progress-bar-background";
        const string k_ProgressBarColorUssClassName = "download-progress-bar";
        const string k_ProgressBarGridItemUssClassName = Constants.GridItemStyleClassName + "-download_progress_bar";
        const string k_ProgressBarDetailsPageUssClassName = "details-page-download-progress-bar";
        const string k_ProgressBarDetailsPageContainerUssClassName = "details-page-download-progress-container";
        const string k_ProgressBarDetailsPageCancelButtonUssClassName = "details-page-download-cancel-button";

        readonly VisualElement m_ProgressBar;

        float m_AnimationLeftOffset = 0f;
        readonly IVisualElementScheduledItem m_AnimationUpdate;

        bool m_IsIndefinite;

        bool IsIndefinite
        {
            get => m_IsIndefinite;

            set
            {
                if (!value)
                {
                    m_AnimationUpdate.Pause();
                }

                if (m_IsIndefinite == value)
                    return;

                m_IsIndefinite = value;

                if (!m_IsIndefinite)
                    return;

                m_AnimationLeftOffset = 0.0f;
                m_AnimationUpdate.Resume();
            }
        }

        public OperationProgressBar(IPageManager pageManager, IAssetImporter assetImporter, bool isCancellable = false)
        {
            UIElementsUtils.Show(this);

            var progressBarContainer = new VisualElement();
            m_ProgressBar = new VisualElement();

            Add(progressBarContainer);
            progressBarContainer.Add(m_ProgressBar);

            progressBarContainer.AddToClassList(k_ProgressBarBackgroundUssClassName);
            m_ProgressBar.AddToClassList(k_ProgressBarColorUssClassName);

            if (!isCancellable)
            {
                progressBarContainer.AddToClassList(k_ProgressBarGridItemUssClassName);
                progressBarContainer.AddToClassList(k_ProgressBarContainerUssClassName);
            }
            else
            {
                var cancelButton = new Button();
                Add(cancelButton);

                AddToClassList(k_ProgressBarDetailsPageContainerUssClassName);
                AddToClassList(k_ProgressBarContainerUssClassName);
                AddToClassList(k_ProgressBarDetailsPageUssClassName);

                cancelButton.AddToClassList(k_ProgressBarDetailsPageCancelButtonUssClassName);
                cancelButton.RemoveFromClassList("unity-button");
                cancelButton.tooltip = L10n.Tr(Constants.CancelImportActionText);
                cancelButton.clicked += () =>
                {
                    assetImporter.CancelImport(pageManager.activePage.selectedAssetId, true);
                };
            }

            m_AnimationUpdate = schedule.Execute(UpdateProgressBar).Every(30);
            IsIndefinite = false;
        }

        void UpdateProgressBar(TimerState timerState)
        {
            if (!m_IsIndefinite)
                return;

            m_AnimationLeftOffset = (m_AnimationLeftOffset + 0.001f * timerState.deltaTime) % 1.0f;

            m_ProgressBar.style.width = Length.Percent(Mathf.Min(m_AnimationLeftOffset + 0.3f, 1.0f - m_AnimationLeftOffset) * 100.0f);
            m_ProgressBar.style.left = Length.Percent(m_AnimationLeftOffset * 100.0f);
        }

        internal void Refresh(BaseOperation operation)
        {
            if (operation?.Status == OperationStatus.InProgress)
            {
                UIElementsUtils.Show(this);
                IsIndefinite = operation.Progress <= 0.0f;

                if (!IsIndefinite)
                {
                    m_ProgressBar.style.left = 0.0f;
                    m_ProgressBar.style.width = Length.Percent(operation.Progress * 100);
                }
            }
            else
            {
                UIElementsUtils.Hide(this);
                m_IsIndefinite = false;
            }
        }
    }
}