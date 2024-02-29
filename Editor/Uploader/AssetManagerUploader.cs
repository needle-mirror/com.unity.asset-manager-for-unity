using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Unity.AssetManager.Editor
{
    class AssetManagerUploader
    {
        readonly UploadSettings m_Settings;

        public AssetManagerUploader(UploadSettings settings)
        {
            m_Settings = settings;
        }

        public static IEnumerable<IUploadAssetEntry> GenerateAssetEntries(IEnumerable<string> mainAssetGuids, bool bundleDependencies)
        {
            var processedGuids = new HashSet<string>();

            var uploadEntries = new List<IUploadAssetEntry>();

            foreach (var assetGuid in mainAssetGuids)
            {
                if (processedGuids.Contains(assetGuid))
                    continue;

                uploadEntries.Add(CreateUploadAssetEntry(assetGuid, bundleDependencies));
                processedGuids.Add(assetGuid);
            }

            return uploadEntries;
        }

        public async Task UploadAssetEntries(IReadOnlyCollection<IUploadAssetEntry> uploadNodes)
        {
            var database = uploadNodes.ToDictionary(node => node.Guid);
            await UploadAssetEntryUploader.Upload(uploadNodes, m_Settings, database);
        }

        static IUploadAssetEntry CreateUploadAssetEntry(string assetGuid, bool bundleDependencies)
        {
            return new AssetUploadEntry(assetGuid, bundleDependencies);
        }

        static class UploadAssetEntryUploader
        {
            public static async Task Upload(IEnumerable<IUploadAssetEntry> nodes, UploadSettings settings, IReadOnlyDictionary<string, IUploadAssetEntry> database)
            {
                var tasks = new List<Task>();
                var taskDispatcher = new UploadTaskDispatcher();

                foreach (var node in nodes)
                {
                    tasks.Add(UploadRecursive(node, settings, taskDispatcher, database));
                }

                // TODO Check for errors
                await Task.WhenAll(tasks);
            }

            static async Task UploadRecursive(IUploadAssetEntry assetEntry, UploadSettings settings, UploadTaskDispatcher taskDispatcher, IReadOnlyDictionary<string, IUploadAssetEntry> database)
            {
                var tasks = new List<Task>();

                foreach (var id in assetEntry.Dependencies)
                {
                    if (database.TryGetValue(id, out var child))
                    {
                        tasks.Add(UploadRecursive(child, settings, taskDispatcher, database));
                    }
                }

                await Task.WhenAll(tasks);

                await taskDispatcher.Start(assetEntry, settings);
            }
        }
    }
}