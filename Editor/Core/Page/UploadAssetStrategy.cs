using System.Collections.Generic;
using System.Linq;

namespace Unity.AssetManager.Editor
{
    static class UploadAssetStrategy
    {
        public static IEnumerable<IUploadAsset> GenerateUploadAssets(IEnumerable<string> guids, IReadOnlyCollection<string> ignoredGuids,
            UploadDependencyMode dependencyMode, UploadFilePathMode filePathMode)
        {
            var processedGuids = new HashSet<string>();

            var uploadAssets = new List<IUploadAsset>();

            foreach (var guid in guids)
            {
                if (processedGuids.Contains(guid))
                    continue;

                IEnumerable<string> dependencyGuids = null;

                var files = new List<string> { guid };

                switch (dependencyMode)
                {
                    case UploadDependencyMode.Embedded:
                        files.AddRange(Utilities.GetValidAssetDependencyGuids(guid, true));
                        break;

                    case UploadDependencyMode.Separate:
                        dependencyGuids = Utilities.GetValidAssetDependencyGuids(guid, false).ToList();
                        break;
                }

                var filteredFiles = files.Where(fileGuid => fileGuid == guid || !ignoredGuids.Contains(fileGuid)).ToList();
                var filteredDependencies = dependencyGuids?.Where(fileGuid => !ignoredGuids.Contains(fileGuid)).ToList();

                var assetUploadEntry = UploadAssetFactory.CreateUnityUploadAsset(guid, filteredFiles,
                    filteredDependencies, filePathMode);

                uploadAssets.Add(assetUploadEntry);
                processedGuids.Add(guid);
            }

            return uploadAssets;
        }
    }
}