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

        bool m_DragSelectionContainsValidAsset;
        
        public DragFromOutsideManipulator(VisualElement rootTarget, IPageManager pageManager)
        {
            target = rootTarget;
            m_PageManager = pageManager;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<DragPerformEvent>(OnDragPerform);
            target.RegisterCallback<DragUpdatedEvent>(OnDragUpdate);
            target.RegisterCallback<DragEnterEvent>(OnDragEnter);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<DragPerformEvent>(OnDragPerform);
            target.UnregisterCallback<DragUpdatedEvent>(OnDragUpdate);
            target.UnregisterCallback<DragEnterEvent>(OnDragEnter);
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
            uploadPage?.AddAssets(DragAndDrop.objectReferences.Where(o => 
                !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(o))).ToList());
        }

        void OnDragUpdate(DragUpdatedEvent _)
        {
            DragAndDrop.visualMode = m_DragSelectionContainsValidAsset
                ? DragAndDropVisualMode.Generic
                : DragAndDropVisualMode.Rejected;
        }

        void OnDragEnter(DragEnterEvent _)
        {
            m_DragSelectionContainsValidAsset = !Array.TrueForAll(DragAndDrop.objectReferences,
                o => string.IsNullOrEmpty(AssetDatabase.GetAssetPath(o)));
        }
    }
}