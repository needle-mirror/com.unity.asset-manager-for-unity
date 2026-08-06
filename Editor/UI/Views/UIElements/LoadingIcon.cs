using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class LoadingIcon : VisualElement
    {
        const string k_StyleSheetClassName = "loading-icon";

        // Height must be set programmatically for rotation of icon
        const int k_Height = 20;
        const int k_Width = k_Height;

        static float s_LastAngle;

        readonly IVisualElementScheduledItem m_Scheduler;
        float m_CurrentAngle;

        internal LoadingIcon()
        {
            AddToClassList(k_StyleSheetClassName);

            style.height = k_Height;
            style.width = k_Width;

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);

            m_Scheduler = schedule.Execute(UpdateAnimation).Every(50);
            m_Scheduler.Pause();
        }

        public void PlayAnimation()
        {
            m_Scheduler.Resume();
        }

        public void StopAnimation()
        {
            m_Scheduler.Pause();
        }

        void UpdateAnimation(TimerState timerState)
        {
            if (style.visibility == Visibility.Hidden)
                return;

            style.rotate = new Rotate(new Angle(m_CurrentAngle, AngleUnit.Degree));
            m_CurrentAngle += 0.6f * timerState.deltaTime;
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_CurrentAngle = s_LastAngle;
            style.rotate = new Rotate(new Angle(m_CurrentAngle, AngleUnit.Degree));
        }

        void OnDetachFromPanel(DetachFromPanelEvent e)
        {
            s_LastAngle = m_CurrentAngle;
        }
    }
}
