using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    /// <summary>
    /// Manipulator that fires a Drag Event when you press the pointer down on its target.
    /// </summary>
    internal class DragStartManipulator : PointerManipulator
    {
        event Action m_OnDragStart;

        internal DragStartManipulator(VisualElement root, Action onDragStart)
        {
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
            target = root;
            m_OnDragStart = onDragStart;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
        }

        internal void OnPointerDown(PointerDownEvent e)
        {
            if (CanStartManipulation(e))
            {
                e.StopPropagation();
                m_OnDragStart?.Invoke();
            }
        }
    }
}
