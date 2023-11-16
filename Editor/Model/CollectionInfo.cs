using System;

namespace Unity.AssetManager.Editor
{
    [Serializable]
    internal class CollectionInfo
    {
        public string organizationId;
        public string projectId;
        public string name;
        public string parentPath;

        public string GetFullPath() => string.IsNullOrEmpty(parentPath) ? name ?? string.Empty : $"{parentPath}/{name ?? string.Empty}";
        public string GetUniqueIdentifier()
        {
            if (string.IsNullOrEmpty(organizationId) || string.IsNullOrEmpty(projectId))
                return string.Empty;
            return $"{organizationId}/{projectId}/{GetFullPath()}";
        }

        public static bool AreEquivalent(CollectionInfo left, CollectionInfo right)
        {
            var leftIdentifier = left?.GetUniqueIdentifier() ?? string.Empty;
            var rightIdentifier = right?.GetUniqueIdentifier() ?? string.Empty;
            return leftIdentifier == rightIdentifier;
        }

        public static CollectionInfo CreateFromFullPath(string fullPath)
        {
            if (!string.IsNullOrEmpty(fullPath))
            {
                var slashIndex = fullPath.LastIndexOf('/');
                if (slashIndex > 0)
                {
                    var parentPath = fullPath.Substring(0, slashIndex);
                    var name = fullPath.Substring(slashIndex + 1);
                    return new CollectionInfo {name = name, parentPath = parentPath};
                }
            }
            return new CollectionInfo { name = fullPath, parentPath = string.Empty };
        }
    }
}