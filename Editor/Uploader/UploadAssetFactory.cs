using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.AssetManager.Editor
{
    static class UploadAssetFactory
    {
        public static IUploadAsset CreateUnityUploadAsset(string primaryAssetGuid, IEnumerable<string> fileAssetGuids,
            IEnumerable<string> dependencyGuids, UploadFilePathMode filePathMode)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(primaryAssetGuid);
            var name = Path.GetFileNameWithoutExtension(assetPath);

            var tags = TagExtractor.ExtractFromAsset(assetPath).ToList();

            var filePaths = new List<string>();

            if (fileAssetGuids != null)
            {
                foreach (var guid in fileAssetGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    filePaths.Add(path);
                }
            }

            var processedPaths = new HashSet<string>();
            var commonPath = filePathMode == UploadFilePathMode.Compact ? Utilities.ExtractCommonFolder(filePaths) : null;

            var files = new List<IUploadFile>();

            foreach (var filePath in filePaths)
            {
                var sanitizedPath = filePath.Replace('\\', '/').ToLower();

                if (processedPaths.Contains(sanitizedPath))
                    continue;

                processedPaths.Add(sanitizedPath);

                if (!AddAssetAndItsMetaFile(files, filePath, commonPath, filePathMode))
                {
                    Debug.LogWarning($"Asset {filePath} is already added to the upload list.");
                }
            }

            return new UploadAsset(name, primaryAssetGuid, files, tags, dependencyGuids);
        }

        static bool AddAssetAndItsMetaFile(IList<IUploadFile> addedFiles, string assetPath, string commonPath, UploadFilePathMode filePathMode)
        {
            string dst;

            switch (filePathMode)
            {
                case UploadFilePathMode.Compact:
                    if (string.IsNullOrEmpty(commonPath))
                    {
                        dst = Utilities.GetPathRelativeToAssetsFolder(assetPath);
                    }
                    else
                    {
                        var normalizedPath = Utilities.NormalizePathSeparators(assetPath);
                        var commonPathNormalized = Utilities.NormalizePathSeparators(commonPath);

                        Utilities.DevAssert(normalizedPath.StartsWith(commonPathNormalized));
                        dst = normalizedPath[commonPathNormalized.Length..];
                    }

                    break;

                case UploadFilePathMode.Flatten:
                    dst = GetFlattenPath(addedFiles, assetPath);
                    break;

                default:
                    dst = Utilities.GetPathRelativeToAssetsFolder(assetPath);
                    break;
            }


            if (addedFiles.Any(e => Utilities.ComparePaths(e.DestinationPath, assetPath)))
            {
                return false;
            }

            addedFiles.Add(new UploadFile(assetPath, dst));
            addedFiles.Add(new UploadFile(MetafilesHelper.AssetMetaFile(assetPath), dst + MetafilesHelper.MetaFileExtension));

            return true;
        }

        static string GetFlattenPath(ICollection<IUploadFile> files, string assetPath)
        {
            var fileName = Path.GetFileName(assetPath);
            return Utilities.GetUniqueFilename(files.Select(e => e.DestinationPath).ToArray(), fileName);
        }
    }
}