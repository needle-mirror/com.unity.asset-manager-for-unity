using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    class OperationProgressBar : VisualElement
    {
        static class UssStyles
        {
            public static readonly string k_ProgressBarContainer = "download-progress-bar-container";
            public static readonly string k_ProgressBarBackground = "download-progress-bar-background";
            public static readonly string k_ProgressBarColor = "download-progress-bar";
            public static readonly string k_ProgressBarError = "download-progress-bar--error";
            public static readonly string k_ProgressBarSuccess = "download-progress-bar--success";
            public static readonly string k_ProgressBarWarning = "download-progress-bar--warning";
            public static readonly string k_ProgressBarGridItem = "grid-view--item-download_progress_bar";
            public static readonly string k_ProgressBarDetailsPage = "details-page-download-progress-bar";
            public static readonly string k_ProgressBarDetailsPageContainer = "details-page-download-progress-container";
            public static readonly string k_ProgressBarDetailsPageCancelButton = "details-page-download-cancel-button";
        }

        readonly IVisualElementScheduledItem m_AnimationUpdate;
        readonly VisualElement m_ProgressBar;
        readonly Button m_CancelButton;

        float m_AnimationLeftOffset;
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

            progressBarContainer.AddToClassList(UssStyles.k_ProgressBarBackground);
            m_ProgressBar.AddToClassList(UssStyles.k_ProgressBarColor);

            if (cancelCallback == null)
            {
                progressBarContainer.AddToClassList(UssStyles.k_ProgressBarGridItem);
                progressBarContainer.AddToClassList(UssStyles.k_ProgressBarContainer);
            }
            else
            {
                m_CancelButton = new Button();
                Add(m_CancelButton);

                AddToClassList(UssStyles.k_ProgressBarDetailsPageContainer);
                AddToClassList(UssStyles.k_ProgressBarContainer);
                AddToClassList(UssStyles.k_ProgressBarDetailsPage);

                m_CancelButton.AddToClassList(UssStyles.k_ProgressBarDetailsPageCancelButton);
                m_CancelButton.RemoveFromClassList("unity-button");
                m_CancelButton.tooltip = L10n.Tr(Constants.CancelImportActionText);
                m_CancelButton.clicked += cancelCallback.Invoke;
            }

            m_AnimationUpdate = schedule.Execute(UpdateProgressBar).Every(30);
            IsIndefinite = false;
        }

        void UpdateProgressBar(TimerState timerState)
        {
            if (!m_IsIndefinite)
                return;

            m_AnimationLeftOffset = (m_AnimationLeftOffset + 0.001f * timerState.deltaTime) % 1.0f;

            m_ProgressBar.style.width =
                Length.Percent(Mathf.Min(m_AnimationLeftOffset + 0.3f, 1.0f - m_AnimationLeftOffset) * 100.0f);
            m_ProgressBar.style.left = Length.Percent(m_AnimationLeftOffset * 100.0f);
        }

        internal void Refresh(BaseOperation operation)
        {
            if (operation == null)
            {
                Hide();
                return;
            }

            switch (operation.Status)
            {
                case OperationStatus.Success:
                    m_CancelButton?.SetEnabled(false);
                    m_ProgressBar.AddToClassList(UssStyles.k_ProgressBarSuccess);
                    m_ProgressBar.RemoveFromClassList(UssStyles.k_ProgressBarColor);
                    m_ProgressBar.RemoveFromClassList(UssStyles.k_ProgressBarError);
                    break;

                case OperationStatus.Error:
                    m_CancelButton?.SetEnabled(false);
                    m_ProgressBar.AddToClassList(UssStyles.k_ProgressBarError);
                    m_ProgressBar.RemoveFromClassList(UssStyles.k_ProgressBarColor);
                    m_ProgressBar.RemoveFromClassList(UssStyles.k_ProgressBarSuccess);
                    break;

                case OperationStatus.Paused:
                    m_CancelButton?.SetEnabled(false);
                    m_ProgressBar.AddToClassList(UssStyles.k_ProgressBarWarning);
                    m_ProgressBar.RemoveFromClassList(UssStyles.k_ProgressBarColor);
                    m_ProgressBar.RemoveFromClassList(UssStyles.k_ProgressBarSuccess);
                    break;

                default:
                    m_ProgressBar.AddToClassList(UssStyles.k_ProgressBarColor);
                    m_ProgressBar.RemoveFromClassList(UssStyles.k_ProgressBarError);
                    m_ProgressBar.RemoveFromClassList(UssStyles.k_ProgressBarSuccess);
                    m_ProgressBar.RemoveFromClassList(UssStyles.k_ProgressBarWarning);
                    break;
            }

            if (operation.Status == OperationStatus.InProgress)
            {
                m_CancelButton?.SetEnabled(true);
                SetProgress(operation.Progress);
            }
            else
            {
                if (operation.IsSticky || operation.Status == OperationStatus.Error)
                {
                    SetProgress(1.0f);
                }
                else if(operation.Status == OperationStatus.Paused)
                {
                    if (m_IsIndefinite)
                    {
                        SetProgress(1.0f);
                    }
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
