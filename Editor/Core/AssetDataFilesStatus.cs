using System;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal enum AssetDataFilesStatus
    {
        Fetched,
        BeingFetched,
        NotFetched
    }
}