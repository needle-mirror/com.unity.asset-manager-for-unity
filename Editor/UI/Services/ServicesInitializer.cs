using System.Linq;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Upload.Editor;
using UnityEditor;
using UnityEngine;
using AssetImporter = Unity.AssetManager.Core.Editor.AssetImporter;

namespace Unity.AssetManager.UI.Editor
{
    static class ServicesInitializer
    {
        [InitializeOnLoadMethod]
        static void Init()
        {
            InitializeServices();

            // For backwards compatibility, register services that did not previously exist.
            ServicesContainer.instance.TryInitializeServices(
                new DialogManager(),
                new FileUtility(),
                new SavedAssetSearchFilterManager(),
                new PersistenceManager(),
                new AssetDataCacheManager(),
                new AssetDataCacheSyncService(),
                new InlineEditService(),
                new UIPreferences());
        }

        public static void ResetServices(IService[] overrides = null)
        {
            InitializeServices(true, overrides);
        }

        static void InitializeServices(bool forceReset = false, IService[] overrides = null)
        {
            if (!forceReset && ServicesContainer.instance.IsInitialized())
            {
                Utilities.DevLog("Services already initialized");
                return;
            }

            IService[] services =
            {
                // Core
                new IOProxy(),
                new ApplicationProxy(),
                new AssetOperationManager(),
                new DownloadManager(),
                new UploadManager(),
                new CachePathHelper(),
                new AssetManagerSettingsManager(),
                new CacheEvictionManager(),
                new ThumbnailDownloader(),
                new UnityConnectProxy(),
                new AssetsSdkProvider(),
                new ProjectOrganizationProvider(),
                new AssetDataManager(),
                new AssetDataCacheManager(),
                new AssetDataCacheSyncService(),
                new ProjectIconDownloader(),
                new AssetDatabaseProxy(),
                new PersistenceManager(),
                new ImportedAssetsTracker(),
                new EditorUtilityProxy(),
                new AssetImporter(),
                new AssetImportResolver(),
                new PermissionsManager(),
                new UrlProvider(),
                new UIPreferences(),
                new DragAndDropProjectBrowserProxy(),
                new FileUtility(),
                new MessageManager(),
                new SavedAssetSearchFilterManager(),
                new PackageVersionService(),
                new InlineEditService(),

                // UI
                new StateManager(),
                new LinksProxy(),
                new PopupManager(),
                new PageManager(),
                new ContextMenuBuilder(),
                new DialogManager(),
                new ProjectWindowProxy(),
                new ProjectWindowIconOverlay()
            };

            if (overrides != null && overrides.Length > 0)
            {
                for (int i = 0; i < services.Length; i++)
                {
                    if (overrides.FirstOrDefault(s => s.RegistrationType == services[i].RegistrationType) is { } overrideService)
                    {
                        services[i] = overrideService;
                    }
                }
            }

            ServicesContainer.instance.InitializeServices(services);

            // Post-initialization configurations
            var assetImportResolver = ServicesContainer.instance.Resolve<IAssetImportResolver>();
            if (assetImportResolver != null)
            {
                assetImportResolver.SetConflictResolver(new AssetImportDecisionMaker());
            }
        }
    }
}
