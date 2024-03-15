using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    interface IUploadManager : IService
    {
        Task UploadAsync(IUploadAssetEntry assetEntry, UploadSettings settings, IDictionary<string, string> assetIdDatabase);
    }

    enum AssetUploadMode
    {
        // TODO Communicates with the user to confirm if the current asset is identical to the one on the cloud
        IgnoreAlreadyUploadedAssets, // Skips the upload if an asset with the same ID already exists on the cloud
        OverrideExistingAssets, // Replaces and overrides any existing asset with the same ID on the cloud
        DuplicateExistingAssets, // Uploads new assets and potentially duplicates without checking for existing matches
    }

    [Serializable]
    class UploadSettings
    {
        public string OrganizationId;
        public string ProjectId;
        public string CollectionPath; //TODO Use a list
        public AssetUploadMode AssetUploadMode = AssetUploadMode.IgnoreAlreadyUploadedAssets;
    }

    [Serializable]
    class UploadManager : BaseService<IUploadManager>, IUploadManager
    {
        const int k_MaxConcurrentUploadTasks = 10;
        static readonly SemaphoreSlim s_UploadAssetSemaphore = new(k_MaxConcurrentUploadTasks);

        [SerializeReference]
        IAssetOperationManager m_AssetOperationManager;

        [ServiceInjection]
        public void Inject(IAssetOperationManager assetOperationManager)
        {
            m_AssetOperationManager = assetOperationManager;
        }

        public async Task UploadAsync(IUploadAssetEntry assetEntry, UploadSettings settings, IDictionary<string, string> assetIdDatabase)
        {
            await s_UploadAssetSemaphore.WaitAsync();

            var operation = new UploadOperation(assetEntry, settings);
            m_AssetOperationManager.RegisterOperation(operation);

            operation.Start();

            try
            {
                await operation.UploadAsync(assetIdDatabase);
                operation.Finish(OperationStatus.Success);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                operation.Finish(OperationStatus.Error);
                throw;
            }
            finally
            {
                s_UploadAssetSemaphore.Release();
            }
        }
    }
}