using UnityEditor;

namespace Unity.AssetManager.Core.Editor
{
    interface IDialogManager
    {
        bool DisplayDialog(string title, string message, string ok);

        string OpenFolderPanel(string title, string folder, string defaultName);
    }

    class DialogManager : IDialogManager
    {
        public bool DisplayDialog(string title, string message, string ok)
        {
            return EditorUtility.DisplayDialog(title, message, ok);
        }

        public string OpenFolderPanel(string title, string folder, string defaultName)
        {
            return EditorUtility.OpenFolderPanel(title, folder, defaultName);
        }
    }
}
