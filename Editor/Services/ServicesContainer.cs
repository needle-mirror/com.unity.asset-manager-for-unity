using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.AssetManager.Editor
{
    internal interface IService
    {
        IReadOnlyCollection<IService> dependencies { get; }
        bool enabled { get; set; }
        Type registrationType { get; }
    }

    internal abstract class BaseService : IService
    {
        private readonly List<IService> m_Dependencies = new List<IService>();
        public IReadOnlyCollection<IService> dependencies => m_Dependencies;
        public abstract Type registrationType { get; }

        protected T RegisterDependency<T>(T dependency) where T : class, IService
        {
            if (dependency == null)
                throw new ArgumentNullException(nameof(dependency));

            m_Dependencies.Add(dependency);
            return dependency;
        }

        private bool m_Enabled;

        public bool enabled
        {
            get => m_Enabled;
            set
            {
                if (m_Enabled == value)
                    return;
                if (value)
                    OnEnable();
                else
                    OnDisable();
                m_Enabled = value;
            }
        }

        public virtual void OnEnable() { }

        public virtual void OnDisable() { }
    }

    internal abstract class BaseService<T> : BaseService where T : IService
    {
        public override Type registrationType => typeof(T);
    }

    [Serializable]
    [ExcludeFromCodeCoverage]
    internal sealed class ServicesContainer : ScriptableSingleton<ServicesContainer>
    {
        [SerializeField]
        private StateManager m_SerializedStateManager;
        [SerializeField]
        private UnityConnectProxy m_SerializedUnityConnectProxy;
        [SerializeField]
        private DownloadManager m_SerializedDownloadManager;
        [SerializeField]
        private AssetsSdkProvider m_SerializedAssetsSdkProvider;
        [SerializeField]
        private PageManager m_SerializedPageManager;
        [SerializeField]
        private AssetDataManager m_SerializedAssetDataManager;
        [SerializeField]
        private ImportedAssetsTracker m_SerializedImportedAssetsTracker;
        [FormerlySerializedAs("m_SerializedAssetImporter")] [SerializeField]
        private AssetImporter serializedAssetImporter;
        [SerializeField]
        private ThumbnailDownloader m_SerializedThumbnailDownloader;
        [SerializeField]
        private ProjectOrganizationProvider m_SerializedProjectOrganizationProvider;

        private readonly Dictionary<Type, IService> m_RegisteredServices = new Dictionary<Type, IService>();

        public ServicesContainer()
        {
            Reload();
        }

        public void Reload()
        {
            m_RegisteredServices.Clear();

            var settingsManager = Register(new AssetManagerSettingsManager());
            var fileInfoWrapper = Register(new FileInfoWrapper());
            var ioProxy = Register(new IOProxy());
            var cacheEvictionManager = Register(new CacheEvictionManager(fileInfoWrapper, settingsManager));
            var webRequestProxy = Register(new WebRequestProxy());
            var editorAnalyticsWrapper = Register(new EditorAnalyticsWrapper());
            var analyticsEngine = Register(new AnalyticsEngine(editorAnalyticsWrapper));
            var unityConnect = Register(new UnityConnectProxy());
            var stateManager = Register(new StateManager());
            var assetDataManager = Register(new AssetDataManager(unityConnect));
            var assetDatabaseProxy = Register(new AssetDatabaseProxy());
            var editorUtilityProxy = Register(new EditorUtilityProxy());
            var downloadManager = Register(new DownloadManager(webRequestProxy, ioProxy));
            var thumbnailDownloader = Register(new ThumbnailDownloader(downloadManager, ioProxy, settingsManager, cacheEvictionManager));
            var importedAssetsTracker = Register(new ImportedAssetsTracker(ioProxy, assetDatabaseProxy, assetDataManager));
            var assetsSdkProvider = Register(new AssetsSdkProvider(assetDataManager, unityConnect));
            var projectOrganizationProvider = Register(new ProjectOrganizationProvider(unityConnect, assetsSdkProvider));
            var linksProxy = Register(new LinksProxy(projectOrganizationProvider));
            var pageManager = Register(new PageManager(unityConnect, assetsSdkProvider, assetDataManager, projectOrganizationProvider));
            var assetImporter = Register(new AssetImporter(assetsSdkProvider, downloadManager, analyticsEngine, ioProxy, assetDatabaseProxy, editorUtilityProxy, importedAssetsTracker, assetDataManager));

            Register(new IconFactory());

            // We need to save some services as serialized members for them to survive domain reload properly
            m_SerializedUnityConnectProxy = unityConnect;
            m_SerializedStateManager = stateManager;
            m_SerializedDownloadManager = downloadManager;
            m_SerializedAssetsSdkProvider = assetsSdkProvider;
            m_SerializedPageManager = pageManager;
            m_SerializedAssetDataManager = assetDataManager;
            m_SerializedImportedAssetsTracker = importedAssetsTracker;
            serializedAssetImporter = assetImporter;
            m_SerializedThumbnailDownloader = thumbnailDownloader;
            m_SerializedProjectOrganizationProvider = projectOrganizationProvider;
        }

        public void OnDisable()
        {
            foreach (var service in m_RegisteredServices.Values)
                service.enabled = false;
        }

        public T Register<T>(T service) where T : class, IService
        {
            if (service == null)
                return null;
            m_RegisteredServices[typeof(T)] = service;
            var registrationType = service.registrationType;
            if (registrationType != null)
                m_RegisteredServices[registrationType] = service;
            return service;
        }

        public T Resolve<T>() where T : class, IService
        {
            var service = m_RegisteredServices.TryGetValue(typeof(T), out var result) ? result as T : null;
            EnableService(service);
            return service;
        }

        private void EnableService(IService service)
        {
            if (service == null || service.enabled)
                return;
            foreach (var dependency in service.dependencies ?? Array.Empty<IService>())
                EnableService(dependency);
            service.enabled = true;
        }

        public void EnableAllServices()
        {
            foreach (var service in m_RegisteredServices.Values)
                EnableService(service);
        }
    }
}
