using System;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class ImportedFileInfo
    {
        public string guid;
        public string originalPath;
        
        public ImportedFileInfo(string guid, string originalPath)
        {
            this.guid = guid;
            this.originalPath = originalPath;
        }
    }
}