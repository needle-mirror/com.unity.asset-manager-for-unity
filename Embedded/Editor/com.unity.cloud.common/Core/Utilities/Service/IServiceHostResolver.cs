using System;
using System.Collections.Generic;

namespace Unity.Cloud.CommonEmbedded
{
    /// <summary>
    /// Resolves the service environment and domain provider for the application.
    /// </summary>
    interface IServiceHostResolver
    {
        /// <summary>
        /// Gets the resolved <see cref="ServiceEnvironment"/>.
        /// </summary>
        /// <returns>The resolved environment.</returns>
        ServiceEnvironment GetResolvedEnvironment();

        /// <summary>
        /// Gets the resolved the <see cref="ServiceDomainProvider"/>.
        /// </summary>
        /// <returns>The resolved service domain provider.</returns>
        ServiceDomainProvider GetResolvedDomainProvider();

        /// <summary>
        /// Returns the service address for the specified inputs.
        /// </summary>
        /// <param name="protocol">The web protocol.</param>
        /// <returns>The service address.</returns>
        string GetResolvedAddress(ServiceProtocol protocol = ServiceProtocol.Http);

        /// <summary>
        /// Resolves and returns the request URI for the specified path and protocol.
        /// </summary>
        /// <param name="path">The request path.</param>
        /// <param name="protocol">The web protocol.</param>
        /// <returns>The Uri string.</returns>
        string GetResolvedRequestUri(string path, ServiceProtocol protocol = ServiceProtocol.Http);
    }
}
