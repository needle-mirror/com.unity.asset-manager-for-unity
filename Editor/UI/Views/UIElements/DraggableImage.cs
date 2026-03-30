using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// VisualElement Image that can support draggable behaviour
    /// Used in places like the gridview and Details Page
    /// </summary>
#if UNITY_6000_6_OR_NEWER
    [UxmlElement]
    partial
#endif
    class DraggableImage : Image
    {
        readonly ClickOrDragStartManipulator m_DragManipulator;

#if !UNITY_6000_6_OR_NEWER
#pragma warning disable CS0618 // Type or member is obsolete
        public new class UxmlFactory : UxmlFactory<DraggableImage> { }
#pragma warning restore CS0618 // Type or member is obsolete
#endif
        public DraggableImage()
        {
            m_DragManipulator = new ClickOrDragStartManipulator(this, null, null, null);
        }

        public DraggableImage(Texture2D image, Action startDrag) : this()
        {
            this.image = image;
            m_DragManipulator.SetOnDragStart(startDrag);
        }

        internal void SetBackgroundImage(Texture2D newImage)
        {
            image = newImage;
        }

        internal void SetStartDragAction(Action startDrag)
        {
            m_DragManipulator.SetOnDragStart(startDrag);
        }
    }
}
