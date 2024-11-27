using System;

namespace Unity.AssetManager.Core.Editor
{
    [Serializable]
    class ImportedFileInfo
    {
        public string Guid;
        public string OriginalPath;

        public string Checksum;
        public long Timestamp;

        public string MetaFileChecksum;
        public long MetalFileTimestamp;

        public ImportedFileInfo(string guid, string originalPath,
            string checksum = null, long timestamp = 0,
            string metaFileChecksum = null, long metalFileTimestamp = 0)
        {
            Guid = guid;
            OriginalPath = originalPath;
            Checksum = checksum;
            Timestamp = timestamp;
            MetaFileChecksum = metaFileChecksum;
            MetalFileTimestamp = metalFileTimestamp;
        }
    }
}
