using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    /// <summary>
    /// Manipulator that fires a Button Event if you release the pointer without moving it (up as button), or a Drag Event if
    /// you move the pointer after pressing it down.
    /// </summary>
    class ClickOrDragStartManipulator : PointerManipulator
    {
        Action<PointerUpEvent> m_ButtonClicked;
        Action m_DragStart;
        bool m_CanStartDrag;

        internal ClickOrDragStartManipulator(VisualElement root, Action<PointerUpEvent> onButtonClicked, Action onDragStart)
        {
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            activators.Add(new ManipulatorActivationFilter{ modifiers = EventModifiers.Shift });
            activators.Add(new ManipulatorActivationFilter{ modifiers = EventModifiers.Control });
            activators.Add(new ManipulatorActivationFilter{ modifiers = EventModifiers.Command });
            target = root;
            m_ButtonClicked = onButtonClicked;
            m_DragStart = onDragStart;
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

        void OnPointerDown(PointerDownEvent e)
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

        void OnPointerUp(PointerUpEvent e)
        {
            if (CannotCompleteInteraction(e))
                return;

            CompleteInteraction(e);
            m_ButtonClicked?.Invoke(e);
        }
        void OnPointerMove(PointerMoveEvent e)
        {
            if (CannotCompleteInteraction(e) || e.deltaPosition.sqrMagnitude < 5f)
                return;

            CompleteInteraction(e);
            m_DragStart?.Invoke();
        }

        bool CannotCompleteInteraction(IPointerEvent e)
        {
            var canStopManipulation = CanStopManipulation(e);
            return !m_CanStartDrag || !canStopManipulation;
        }

        void CompleteInteraction(EventBase eb)
        {
            m_CanStartDrag = false;
            eb.StopPropagation();
        }

        internal void SetOnDragStart(Action newOnDragStart)
        {
            m_DragStart = newOnDragStart;
        }

        internal void SetOnButtonClicked(Action<PointerUpEvent> newOnButtonClicked)
        {
            m_ButtonClicked = newOnButtonClicked;
        }
    }
}
