using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class ImportProgressBar : VisualElement
    {
        const string k_ProgressBarContainerUssClassName = "download-progress-bar-container";
        const string k_ProgressBarBackgroundUssClassName = "download-progress-bar-background";
        const string k_ProgressBarColorUssClassName = "download-progress-bar";
        const string k_ProgressBarGridItemUssClassName = Constants.GridItemStyleClassName + "-download_progress_bar";
        const string k_ProgressBarDetailsPageUssClassName = "details-page-download-progress-bar";
        const string k_ProgressBarDetailsPageContainerUssClassName = "details-page-download-progress-container";
        const string k_ProgressBarDetailsPageCancelButtonUssClassName = "details-page-download-cancel-button";

        VisualElement m_ProgressBarContainer;
        VisualElement m_ProgressBar;
        Button m_CancelButton;

        readonly IPageManager m_PageManager;
        readonly IAssetImporter m_AssetImporter;

        bool m_IsInfinite = false;
        float m_AnimationLeftOffset = 0f;

        public ImportProgressBar(IPageManager pageManager, IAssetImporter assetImporter, bool isCancellable = false)
        {
            style.display = DisplayStyle.Flex;
            
            m_PageManager = pageManager;
            m_AssetImporter = assetImporter;

            m_ProgressBarContainer = new VisualElement();
            m_ProgressBar = new VisualElement();

            Add(m_ProgressBarContainer);
            m_ProgressBarContainer.Add(m_ProgressBar);

            m_ProgressBarContainer.AddToClassList(k_ProgressBarBackgroundUssClassName);
            m_ProgressBar.AddToClassList(k_ProgressBarColorUssClassName);

            if (!isCancellable)
            {
                m_ProgressBarContainer.AddToClassList(k_ProgressBarGridItemUssClassName);
                m_ProgressBarContainer.AddToClassList(k_ProgressBarContainerUssClassName);
            }
            else
            {
                m_CancelButton = new Button();
                Add(m_CancelButton);

                AddToClassList(k_ProgressBarDetailsPageContainerUssClassName);
                AddToClassList(k_ProgressBarContainerUssClassName);
                AddToClassList(k_ProgressBarDetailsPageUssClassName);

                m_CancelButton.AddToClassList(k_ProgressBarDetailsPageCancelButtonUssClassName);
                m_CancelButton.RemoveFromClassList("unity-button");
                m_CancelButton.tooltip = L10n.Tr("Cancel import");
                m_CancelButton.clicked += () =>
                {
                    m_AssetImporter.CancelImport(m_PageManager.activePage.selectedAssetId, true);
                };
            }
            
            schedule.Execute(UpdateProgressBar).Every(30);
        }
        
        void UpdateProgressBar(TimerState timerState)
        {
            if (!m_IsInfinite)
                return;
            
            m_AnimationLeftOffset = (m_AnimationLeftOffset + 0.001f * timerState.deltaTime) % 1.0f;
            
            m_ProgressBar.style.width = Length.Percent(Mathf.Min(m_AnimationLeftOffset + 0.3f, 1.0f - m_AnimationLeftOffset) * 100.0f);
            m_ProgressBar.style.left = Length.Percent(m_AnimationLeftOffset * 100.0f);
        }

        internal void Refresh(ImportOperation importOperation)
        {
            if (importOperation?.status == OperationStatus.InProgress)
            {
                style.display = DisplayStyle.Flex;
                m_IsInfinite = false;
                m_ProgressBar.style.left = 0.0f;
                m_ProgressBar.style.width = Length.Percent(importOperation.progress * 100);
            }
            else if (importOperation?.status == OperationStatus.InInfiniteProgress)
            {
                style.display = DisplayStyle.Flex;
                m_IsInfinite = true;
                m_AnimationLeftOffset = 0.0f;
            }
            else
            {
                style.display = DisplayStyle.None;
                m_IsInfinite = false;
            }
        }
    }
}