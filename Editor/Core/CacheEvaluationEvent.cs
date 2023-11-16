using System;

namespace Unity.AssetManager.Editor
{
    internal static class CacheEvaluationEvent
    {
        public static event Action<string> evaluateCache = delegate { };
        
        public static void RaiseEvent()
        {
            evaluateCache?.Invoke(string.Empty);
        }
    }
}