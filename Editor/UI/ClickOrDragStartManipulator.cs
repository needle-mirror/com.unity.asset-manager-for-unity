using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    /// <summary>
    /// Manipulator that fires a Button Event if you release the pointer without moving it (up as button), or a Drag Event if you move the pointer after pressing it down.
    /// </summary>
    internal class ClickOrDragStartManipulator : PointerManipulator
    {
        event Action m_OnButtonClicked;
        event Action m_OnDragStart;

        bool m_CanStartDrag = false;

        internal ClickOrDragStartManipulator(VisualElement root, Action onButtonClicked, Action onDragStart)
        {
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            target = root;
            m_OnButtonClicked = onButtonClicked;
            m_OnDragStart = onDragStart;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        }

        internal void OnPointerDown(PointerDownEvent e)
        {
            if (m_CanStartDrag)
            {
                e.StopImmediatePropagation();
                return;
            }

            if (CanStartManipulation(e))
            {
                m_CanStartDrag = true;
                e.StopPropagation();
            }
        }

        internal void OnPointerUp(PointerUpEvent e)
        {
            if (CannotCompleteInteraction(e))
                return;

            CompleteInteraction(e);
            m_OnButtonClicked?.Invoke();
        }

        internal void OnPointerMove(PointerMoveEvent e)
        {
            if (CannotCompleteInteraction(e))
                return;

            CompleteInteraction(e);
            m_OnDragStart?.Invoke();
        }

        bool CannotCompleteInteraction(IPointerEvent e)
        {
            bool canStopManipulation = CanStopManipulation(e);
            return !m_CanStartDrag || !canStopManipulation;
        }

        void CompleteInteraction(EventBase eb)
        {
            m_CanStartDrag = false;
            eb.StopPropagation();
        }

        internal void SetOnDragStart(Action newOnDragStart) => m_OnDragStart = newOnDragStart;
        internal void SetOnButtonClicked(Action newOnButtonClicked) => m_OnButtonClicked = newOnButtonClicked;
    }
}
