using System;
using System.Reflection;
using Unity.Cloud.AssetsEmbedded;
using Unity.Cloud.CommonEmbedded;
using Unity.Cloud.CommonEmbedded.Runtime;
using Unity.Cloud.IdentityEmbedded;
using Unity.Cloud.IdentityEmbedded.Editor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Unity.AssetManager.Core.Editor
{
    /// <summary>
    /// A common base for the implementation of services that requires the cloud SDK.
    /// </summary>
    abstract class BaseSdkService : BaseService
    {
        /// <summary>
        /// An override class that allows the sdk service to be overridden. Only used for testing.
        /// </summary>
        [Serializable]
        internal class SdkServiceOverride
        {
            public IAssetRepository AssetRepository { get; set; }
            public IOrganizationRepository OrganizationRepository { get; set; }

            public Unity.Cloud.IdentityEmbedded.AuthenticationState AuthenticationState { get; set; }

            public Action AuthenticationStateChanged;
        }

        [SerializeReference]
        SdkServiceOverride m_ServiceOverride;

        protected IOrganizationRepository OrganizationRepository => m_ServiceOverride?.OrganizationRepository ?? Services.OrganizationRepository;
        protected IAssetRepository AssetRepository => m_ServiceOverride?.AssetRepository ?? Services.AssetRepository;

        protected BaseSdkService() { }

        /// <summary>
        /// Internal constructor that allows the sdk service to be overridden. Only used for testing.
        /// </summary>
        /// <param name="sdkServiceOverride"></param>
        protected BaseSdkService(SdkServiceOverride sdkServiceOverride)
        {
            m_ServiceOverride = sdkServiceOverride;
        }

        protected void InitAuthenticatedServices()
        {
            if (m_ServiceOverride == null)
            {
                Services.InitAuthenticatedServices();
            }
        }

        protected Unity.Cloud.IdentityEmbedded.AuthenticationState GetAuthenticationState() => m_ServiceOverride?.AuthenticationState ?? Services.AuthenticationState;

        protected void RegisterOnAuthenticationStateChanged(Action callback)
        {
            if (m_ServiceOverride == null)
            {
                Services.AuthenticationStateChanged += callback;
            }
            else
            {
                m_ServiceOverride.AuthenticationStateChanged += callback;
            }
        }

        protected void UnregisterOnAuthenticationStateChanged(Action callback)
        {
            if (m_ServiceOverride == null)
            {
                Services.AuthenticationStateChanged -= callback;
            }
            else
            {
                m_ServiceOverride.AuthenticationStateChanged -= callback;
            }
        }

        static class Services
        {
            static IAssetRepository s_AssetRepository;

            public static IAssetRepository AssetRepository
            {
                get
                {
                    InitAuthenticatedServices();
                    return s_AssetRepository;
                }
            }

            public static IOrganizationRepository OrganizationRepository => UnityEditorServiceAuthorizer.instance;

            public static Unity.Cloud.IdentityEmbedded.AuthenticationState AuthenticationState =>
                UnityEditorServiceAuthorizer.instance.AuthenticationState;

            public static event Action AuthenticationStateChanged;

            public static void InitAuthenticatedServices()
            {
                if (s_AssetRepository == null)
                {
                    CreateServices();
                }
            }

            static void CreateServices()
            {
                var pkgInfo = PackageInfo.FindForAssembly(Assembly.GetAssembly(typeof(Services)));
                var httpClient = new UnityHttpClient();
                var serviceHostResolver = UnityRuntimeServiceHostResolverFactory.Create();

                UnityEditorServiceAuthorizer.instance.AuthenticationStateChanged += OnAuthenticationStateChanged;

                var serviceHttpClient =
                    new ServiceHttpClient(httpClient, UnityEditorServiceAuthorizer.instance, new AppIdProvider())
                        .WithApiSourceHeaders(pkgInfo.name, pkgInfo.version);

                s_AssetRepository = AssetRepositoryFactory.Create(serviceHttpClient, serviceHostResolver,
                    AssetRepositoryCacheConfiguration.NoCaching);
            }

            static void OnAuthenticationStateChanged(Unity.Cloud.IdentityEmbedded.AuthenticationState state)
            {
                AuthenticationStateChanged?.Invoke();
            }

            class AppIdProvider : IAppIdProvider
            {
                public AppId GetAppId()
                {
                    return new AppId();
                }
            }
        }
    }
}
