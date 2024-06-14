using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class DragFromOutsideManipulator : PointerManipulator
    {
        [SerializeReference]
        IPageManager m_PageManager;
        
        public DragFromOutsideManipulator(VisualElement rootTarget, IPageManager pageManager)
        {
            target = rootTarget;
            m_PageManager = pageManager;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<DragUpdatedEvent>(OnDragUpdate);
            target.RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<DragUpdatedEvent>(OnDragUpdate);
            target.UnregisterCallback<DragPerformEvent>(OnDragPerform);
        }
        
        void OnDragUpdate(DragUpdatedEvent _)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
        }

        void OnDragPerform(DragPerformEvent _)
        {
            if(DragAndDrop.objectReferences.Length == 0 || Array.TrueForAll(DragAndDrop.objectReferences, o => o is DraggableObjectToImport))
                return;
            
            DragAndDrop.AcceptDrag();

            if (!(m_PageManager.ActivePage is UploadPage))
            {
                m_PageManager.SetActivePage<UploadPage>();
            }
            
            var uploadPage = m_PageManager.ActivePage as UploadPage;
            uploadPage?.AddAssets(DragAndDrop.objectReferences.ToList());
        }
    }
}