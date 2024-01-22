using System.ComponentModel;
using UnityEditor;

namespace Unity.AssetManager.Editor
{
    internal interface IEditorUtilityProxy : IService
    {
        bool DisplayDialog(string title, string message, string ok, [DefaultValue("\"\"")] string cancel);

        int DisplayDialogComplex(string title, string message, string ok, [DefaultValue("\"\"")] string cancel, string alt);
    }

    internal class EditorUtilityProxy : BaseService<IEditorUtilityProxy>, IEditorUtilityProxy
    {
        public bool DisplayDialog(string title, string message, string ok, [DefaultValue("\"\"")] string cancel)
        {
            return EditorUtility.DisplayDialog(title, message, ok, cancel);
        }

        public int DisplayDialogComplex(string title, string message, string ok, [DefaultValue("\"\"")] string cancel, string alt)
        {
            return EditorUtility.DisplayDialogComplex(title, message, ok, cancel, alt);
        }
    }
}