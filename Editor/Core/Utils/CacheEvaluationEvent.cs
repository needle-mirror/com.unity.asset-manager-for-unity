using System;

namespace Unity.AssetManager.Core.Editor
{
    static class CacheEvaluationEvent
    {
        public static event Action<string> EvaluateCache = delegate { };

        public static void RaiseEvent()
        {
            EvaluateCache?.Invoke(string.Empty);
        }
    }
}
