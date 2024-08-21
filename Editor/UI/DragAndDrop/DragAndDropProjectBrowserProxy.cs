using System;
using UnityEditor;

namespace Unity.AssetManager.Editor
{
    interface IDragAndDropProjectBrowserProxy : IService
    {
        void RegisterProjectBrowserHandler(DragAndDrop.ProjectBrowserDropHandler projectHandlerDelegate);
        void UnRegisterProjectBrowserHandler(DragAndDrop.ProjectBrowserDropHandler projectHandlerDelegate);
    }

    [Serializable]
    class DragAndDropProjectBrowserProxy : BaseService<IDragAndDropProjectBrowserProxy>, IDragAndDropProjectBrowserProxy
    {
        public void RegisterProjectBrowserHandler(DragAndDrop.ProjectBrowserDropHandler projectHandlerDelegate)
        {
            DragAndDrop.AddDropHandler(projectHandlerDelegate);
        }

        public void UnRegisterProjectBrowserHandler(DragAndDrop.ProjectBrowserDropHandler projectHandlerDelegate)
        {
            DragAndDrop.RemoveDropHandler(projectHandlerDelegate);
        }
    }
}