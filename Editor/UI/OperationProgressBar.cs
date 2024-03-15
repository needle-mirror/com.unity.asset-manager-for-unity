using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class OperationProgressBar : VisualElement
    {
        static class Styles
        {
            public static readonly string k_ProgressBarContainer = "download-progress-bar-container";
            public static readonly string k_ProgressBarBackground = "download-progress-bar-background";
            public static readonly string k_ProgressBarColor = "download-progress-bar";
            public static readonly string k_ProgressBarError = "download-progress-bar-error";
            public static readonly string k_ProgressBarGridItem = "grid-view--item-download_progress_bar";
            public static readonly string k_ProgressBarDetailsPage = "details-page-download-progress-bar";
            public static readonly string k_ProgressBarDetailsPageContainer = "details-page-download-progress-container";
            public static readonly string k_ProgressBarDetailsPageCancelButton = "details-page-download-cancel-button";
        }

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

        public OperationProgressBar(Action cancelCallback = null)
        {
            UIElementsUtils.Show(this);

            var progressBarContainer = new VisualElement();
            m_ProgressBar = new VisualElement();

            Add(progressBarContainer);
            progressBarContainer.Add(m_ProgressBar);

            progressBarContainer.AddToClassList(Styles.k_ProgressBarBackground);
            m_ProgressBar.AddToClassList(Styles.k_ProgressBarColor);

            if (cancelCallback == null)
            {
                progressBarContainer.AddToClassList(Styles.k_ProgressBarGridItem);
                progressBarContainer.AddToClassList(Styles.k_ProgressBarContainer);
            }
            else
            {
                var cancelButton = new Button();
                Add(cancelButton);

                AddToClassList(Styles.k_ProgressBarDetailsPageContainer);
                AddToClassList(Styles.k_ProgressBarContainer);
                AddToClassList(Styles.k_ProgressBarDetailsPage);

                cancelButton.AddToClassList(Styles.k_ProgressBarDetailsPageCancelButton);
                cancelButton.RemoveFromClassList("unity-button");
                cancelButton.tooltip = L10n.Tr(Constants.CancelImportActionText);
                cancelButton.clicked += cancelCallback.Invoke;
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

        internal void Refresh(AssetDataOperation operation)
        {
            if (operation == null)
            {
                Hide();
                return;
            }

            if (operation.Status != OperationStatus.Error)
            {
                m_ProgressBar.AddToClassList(Styles.k_ProgressBarColor);
                m_ProgressBar.RemoveFromClassList(Styles.k_ProgressBarError);
            }
            else
            {
                m_ProgressBar.RemoveFromClassList(Styles.k_ProgressBarColor);
                m_ProgressBar.AddToClassList(Styles.k_ProgressBarError);
            }

            if (operation.Status == OperationStatus.InProgress)
            {
                SetProgress(operation.Progress);
            }
            else
            {
                if (operation.IsSticky || operation.Status == OperationStatus.Error)
                {
                    SetProgress(1.0f);
                }
                else
                {
                    Hide();
                }
            }
        }

        void Hide()
        {
            UIElementsUtils.Hide(this);
            m_IsIndefinite = false;
        }

        void SetProgress(float progress)
        {
            UIElementsUtils.Show(this);
            IsIndefinite = progress <= 0.0f;

            if (!IsIndefinite)
            {
                m_ProgressBar.style.left = 0.0f;
                m_ProgressBar.style.width = Length.Percent(progress * 100);
            }
        }
    }
}