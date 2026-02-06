using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Upload.Editor;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    class DependencyFoldoutItemViewModel
    {
        //dependencies
        readonly IPageManager m_PageManager;
        readonly ISettingsManager m_SettingsManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IUnityConnectProxy m_UnityConnectProxy;
        readonly IAssetDataManager m_AssetDataManager;
        CancellationTokenSource m_CancellationTokenSource;

        //events
        public event Action AssetDataChanged;

        //properties
        AssetIdentifier m_AssetIdentifier;
        BaseAssetData m_AssetData;
        Task m_DataPopulationTask;
        Dictionary<string, string> m_VersionsIds = new();
        List<string> m_VersionLabels = new();

        public Dictionary<string, string> VersionIds => m_VersionsIds;
        public List<string> VersionLabels => m_VersionLabels;
        public string AssetId => m_AssetIdentifier?.AssetId ?? string.Empty;
        public string AssetVersionLabel => m_AssetIdentifier?.VersionLabel ?? string.Empty;
        public string AssetVersion => m_AssetIdentifier?.Version ?? string.Empty;

        public BaseAssetData AssetData => m_AssetData;
        public string AssetName => m_AssetData?.Name;
        public string AssetPrimaryExtension => m_AssetData?.PrimaryExtension;
        public AssetDataAttributeCollection AssetAttributes => m_AssetData?.AssetDataAttributeCollection;

        public DependencyFoldoutItemViewModel(IPageManager pageManager, ISettingsManager settingsManager,
            IProjectOrganizationProvider projectOrganizationProvider, IUnityConnectProxy unityConnectProxy,
            IAssetDataManager dataManager)
        {
            m_PageManager = pageManager;
            m_SettingsManager = settingsManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_UnityConnectProxy = unityConnectProxy;
            m_AssetDataManager = dataManager;
        }

        public void NavigateToDependency()
        {
            if(m_AssetData != null && m_AssetData.Identifier != null)
                m_PageManager.ActivePage.SelectAsset(m_AssetData.Identifier, false);
        }

        public bool IsDependencySelectionEnabled()
        {
            return m_PageManager.ActivePage is UploadPage && m_SettingsManager.IsDependencyVersionSelectionEnabled;
        }

        public bool NeedToRefreshVersions()
        {
            return m_VersionsIds.Count == 0 || m_VersionLabels.Count == 0;
        }

        public async Task Bind(AssetIdentifier dependencyIdentifier)
        {
            // When Bind is called multiple times before the first call is finished,
            // we want to await the first call instead of starting a new one, since data won't have changed in that lapse.
            m_DataPopulationTask ??= BindInternal(dependencyIdentifier);

            try
            {
                await m_DataPopulationTask;
            }
            catch (Exception)
            {
                // Ignore exceptions here as they are handled in BindInternal
            }
            finally
            {
                m_DataPopulationTask = null;
            }
        }

        async Task BindInternal(AssetIdentifier dependencyIdentifier)
        {
            try
            {
                m_AssetData = null;
                var assetData = await FetchAssetData(dependencyIdentifier);

                if (!m_UnityConnectProxy
                        .AreCloudServicesReachable) // when offline or unity services are unreachable, we go back to default behaviour or showing local dependency
                {
                    m_AssetData = assetData;
                    m_AssetIdentifier = dependencyIdentifier;

                    AssetDataChanged?.Invoke();
                    return;
                }

                if (assetData is AssetData)
                {
                    // The local dependency vs the actual dependency might not be the same version.
                    // So we fetch the versions and select the correct one. This is both for the upload tab and the project detail tab
                    await assetData.RefreshVersionsAsync();
                    m_AssetData = assetData.Versions
                        .FirstOrDefault(v => v.Identifier.Version == dependencyIdentifier.Version);
                }
                else
                {
                    m_AssetData = assetData;
                }
            }
            catch (OperationCanceledException)
            {
                AssetDataChanged?.Invoke();
                return;
            }
            catch (Exception)
            {
                Utilities.DevLog($"Dependency ({dependencyIdentifier.AssetId}) could not found for asset.");
            }

            m_AssetIdentifier = dependencyIdentifier;
            AssetDataChanged?.Invoke();

            await ResolveData(m_AssetData);
        }

        async Task<BaseAssetData> FetchAssetData(AssetIdentifier identifier)
        {
            // First, if the asset is imported, we want to display the imported version
            var info = m_AssetDataManager.GetImportedAssetInfo(identifier);

            if (info != null)
            {
                return info.AssetData;
            }

            // If not, try to get it from the AssetDataManager cache
            // The AssetDataManager cache can only contain one version per asset (limitation or design choice?)
            // So we need to make sure the version returned is the one we need.
            var assetData = m_AssetDataManager.GetAssetData(identifier);

            if (assetData != null && (assetData.Identifier == identifier || m_PageManager.ActivePage is UploadPage && assetData.Identifier.AssetId == identifier.AssetId))
            {
                return assetData;
            }

            // Otherwise fetch the asset from the server
            var assetsProvider = ServicesContainer.instance.Resolve<IAssetsProvider>();
            assetData = await assetsProvider.GetAssetAsync(identifier, default);

            return assetData;
        }

        async Task ResolveData(BaseAssetData assetData)
        {
            if (assetData == null)
                return;

            var tasks = new[]
            {
                assetData.RefreshAssetDataAttributesAsync(),
                assetData.ResolveDatasetsAsync()
            };

            await Task.WhenAll(tasks);

            if (IsDependencySelectionEnabled())
            {
                var uploadAssetData = (UploadAssetData)assetData;
                if ((uploadAssetData.Versions == null || !uploadAssetData.Versions.Any()) && !uploadAssetData.IsBeingAdded && !uploadAssetData.IsIgnored)
                    await uploadAssetData.RefreshVersionsAsync(CancellationToken.None);

                m_VersionsIds.Clear();
                m_VersionLabels = await m_ProjectOrganizationProvider.GetOrganizationVersionLabelsAsync();

                var versions = !uploadAssetData.IsBeingAdded ? uploadAssetData.Versions?.Where(v => v.SequenceNumber != 0)
                    .OrderByDescending(v => v.SequenceNumber).ToList() : new List<BaseAssetData>();
                if (versions != null)
                {
                    if (uploadAssetData.CanBeUploaded)
                    {
                        m_VersionsIds.Add(Constants.NewVersionText, AssetManagerCoreConstants.NewVersionId);
                    }

                    foreach (var version in versions)
                    {
                        var versionDisplay = $"Ver. {version.SequenceNumber}";
                        m_VersionsIds[versionDisplay] = version.Identifier.Version;
                    }
                }
            }

            AssetDataChanged?.Invoke();
        }

        public void SetAssetVersionAndLabel(string version, string versionLabel)
        {
            m_AssetIdentifier.Version = version;
            m_AssetIdentifier.VersionLabel = versionLabel;
        }
    }
}
