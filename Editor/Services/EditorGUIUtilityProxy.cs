using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    internal interface IEditorGUIUtilityProxy : IService
    {
        void PingObject(Object obj);
    }
    
    internal class EditorGUIUtilityProxy : BaseService<IEditorGUIUtilityProxy>, IEditorGUIUtilityProxy
    {
        public void PingObject(Object obj)
        {
            EditorGUIUtility.PingObject(obj);
        }
    }
}
