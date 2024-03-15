using System.Collections.Generic;
using System.Threading.Tasks;

namespace Unity.AssetManager.Editor
{
    class UploadTaskDispatcher
    {
        readonly Dictionary<string, Task> m_Tasks = new();

        readonly Dictionary<string, string> m_AssetIdDatabase = new();

        readonly IUploadManager m_UploadManager;

        public UploadTaskDispatcher()
        {
            m_UploadManager = ServicesContainer.instance.Resolve<IUploadManager>();
        }

        public Task Start(IUploadAssetEntry assetEntry, UploadSettings settings)
        {
            if (m_Tasks.TryGetValue(assetEntry.Guid, out var task))
            {
                return task;
            }

            var newTask = m_UploadManager.UploadAsync(assetEntry, settings, m_AssetIdDatabase);

            m_Tasks.Add(assetEntry.Guid, newTask);

            return newTask;
        }
    }
}