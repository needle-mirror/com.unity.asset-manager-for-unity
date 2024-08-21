using System;

namespace Unity.AssetManager.Editor.Model
{
    [Serializable]
    class CachedItem
    {
        public string CacheKey { get; set; }
        public string Path { get; set; }
    }
}
