using System;

namespace Unity.Cloud.CommonEmbedded
{
    /// <summary>
    /// A factory class used to create a <see cref="IServiceHostResolver"/>.
    /// </summary>
    static class ServiceHostResolverFactory
    {
        /// <summary>
        /// Create a <see cref="IServiceHostResolver"/> with default values.
        /// Any system-level overrides set via environment variables will take priority.
        /// </summary>
        /// <returns>The created configuration.</returns>
        public static IServiceHostResolver Create()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return new ServiceHostResolver();
#else
            throw new InvalidOperationException("Consider using UnityRuntimeServiceHostResolverFactory from the Unity.Cloud.Common.Runtime assembly");
#endif
        }

        /// <summary>
        /// Create a <see cref="IServiceHostResolver"/> with an optional application-level override for service host options.
        /// Any system-level overrides set via environment variables will take priority.
        /// </summary>
        /// <param name="applicationOverride">An application-level override value for for service host options.</param>
        /// <returns>The created configuration.</returns>
        internal static IServiceHostResolver CreateWithOverride(ServiceHost applicationOverride)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            return new ServiceHostResolver(applicationOverride);
#else
            throw new InvalidOperationException("Consider using UnityRuntimeServiceHostResolverFactory from the Unity.Cloud.Common.Runtime assembly");
#endif
        }
    }
}
