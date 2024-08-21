using System;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    class ImportedFileInfo
    {
        public string Guid;
        public string OriginalPath;

        public ImportedFileInfo(string guid, string originalPath)
        {
            Guid = guid;
            OriginalPath = originalPath;
        }
    }
}
