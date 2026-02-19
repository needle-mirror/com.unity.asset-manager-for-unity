using System;
using Unity.AssetManager.Core.Editor;
using UnityEditor;

namespace Unity.AssetManager.Core.Editor
{
    interface IDragAndDropProjectBrowserProxy : IService
    {
#if UNITY_6000_4_OR_NEWER
        void RegisterProjectBrowserHandler(DragAndDrop.ProjectBrowserDropHandlerV2 projectHandlerDelegate);
        void UnRegisterProjectBrowserHandler(DragAndDrop.ProjectBrowserDropHandlerV2 projectHandlerDelegate);
#else
        void RegisterProjectBrowserHandler(DragAndDrop.ProjectBrowserDropHandler projectHandlerDelegate);
        void UnRegisterProjectBrowserHandler(DragAndDrop.ProjectBrowserDropHandler projectHandlerDelegate);
#endif
    }

    [Serializable]
    class DragAndDropProjectBrowserProxy : BaseService<IDragAndDropProjectBrowserProxy>, IDragAndDropProjectBrowserProxy
    {
#if UNITY_6000_4_OR_NEWER
        public void RegisterProjectBrowserHandler(DragAndDrop.ProjectBrowserDropHandlerV2 projectHandlerDelegate)
        {
            DragAndDrop.AddDropHandlerV2(projectHandlerDelegate);
        }

        public void UnRegisterProjectBrowserHandler(DragAndDrop.ProjectBrowserDropHandlerV2 projectHandlerDelegate)
        {
            DragAndDrop.RemoveDropHandlerV2(projectHandlerDelegate);
        }
#else
        public void RegisterProjectBrowserHandler(DragAndDrop.ProjectBrowserDropHandler projectHandlerDelegate)
        {
            DragAndDrop.AddDropHandler(projectHandlerDelegate);
        }

        public void UnRegisterProjectBrowserHandler(DragAndDrop.ProjectBrowserDropHandler projectHandlerDelegate)
        {
            DragAndDrop.RemoveDropHandler(projectHandlerDelegate);
        }
#endif
    }
}
