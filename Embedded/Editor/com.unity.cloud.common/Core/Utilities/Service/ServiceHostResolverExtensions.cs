using System;

namespace Unity.Cloud.CommonEmbedded
{
    /// <summary>
    /// Helper methods for <see cref="IServiceHostResolver"/>.
    /// </summary>
    static class ServiceHostResolverExtensions
    {
        /// <summary>
        /// Returns an instance of <see cref="ServiceHost"/> initialized with values resolved from the <paramref name="serviceHostResolver"/>.
        /// </summary>
        /// <param name="serviceHostResolver">The <see cref="IServiceHostResolver"/> from which to create a <see cref="ServiceHost"/>.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceHostResolver"/> is null.</exception>
        public static ServiceHost GetResolvedServiceHost(this IServiceHostResolver serviceHostResolver)
        {
            if (serviceHostResolver == null)
                throw new ArgumentNullException(nameof(serviceHostResolver));

            return new ServiceHost
            {
                EnvironmentValue = serviceHostResolver.GetResolvedEnvironment().ToString(),
                ProviderValue = serviceHostResolver.GetResolvedDomainProvider().ToString()
            };
        }

        /// <summary>
        /// Creates a copy of the <paramref name="serviceHostResolver"/> with the given <paramref name="domainResolverOverride"/>.
        /// </summary>
        /// <param name="serviceHostResolver">The <see cref="IServiceHostResolver"/> to copy.</param>
        /// <param name="domainResolverOverride">The <see cref="IServiceDomainResolver"/> to initialize the copy with.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="serviceHostResolver"/> or <paramref name="domainResolverOverride"/> are null.</exception>
        public static IServiceHostResolver CreateCopyWithDomainResolverOverride(this IServiceHostResolver serviceHostResolver, IServiceDomainResolver domainResolverOverride)
        {
            if (serviceHostResolver == null)
                throw new ArgumentNullException(nameof(serviceHostResolver));

            if (domainResolverOverride == null)
                throw new ArgumentNullException(nameof(domainResolverOverride));

            return new ServiceHostResolver(serviceHostResolver.GetResolvedServiceHost(), domainResolverOverride);
        }
    }
}
