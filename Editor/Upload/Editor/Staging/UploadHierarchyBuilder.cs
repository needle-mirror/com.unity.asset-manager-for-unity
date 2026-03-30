using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AssetManager.Core.Editor;

namespace Unity.AssetManager.Upload.Editor
{
    /// <summary>
    /// Static helper class that builds a tree hierarchy from a flat list of UploadAssetData.
    /// </summary>
    static class UploadHierarchyBuilder
    {
        /// <summary>
        /// Builds a hierarchy tree from a flat list of upload assets.
        /// Groups assets by their target project when uploading to multiple projects.
        /// </summary>
        /// <param name="assets">The flat list of assets to organize into a tree</param>
        /// <param name="projectName">Name for the root project node (used when all assets target the same project)</param>
        /// <param name="collectionPath">Optional collection path to use as root</param>
        /// <param name="expandedStates">Dictionary of node IDs to their expanded state</param>
        /// <param name="projectNames">Optional dictionary mapping project IDs to display names</param>
        /// <param name="includeFolders">When true, assets are organized in folder hierarchy; when false, assets are displayed flat under project root</param>
        /// <returns>The root node of the built hierarchy (SuperRoot for multiple projects, ProjectRoot for single project)</returns>
        public static UploadHierarchyNode BuildTree(
            IEnumerable<UploadAssetData> assets,
            string projectName,
            string collectionPath,
            IReadOnlyDictionary<string, bool> expandedStates,
            IReadOnlyDictionary<string, string> projectNames = null,
            bool includeFolders = false)
        {
            var assetList = assets?.ToList() ?? new List<UploadAssetData>();

            if (assetList.Count == 0)
            {
                // Return empty ProjectRoot for no assets
                var emptyRootId = string.IsNullOrEmpty(collectionPath) ? "project_root" : NormalizePath(collectionPath);
                var emptyRootName = string.IsNullOrEmpty(projectName) ? "Project" : projectName;
                return new UploadHierarchyNode(emptyRootId, emptyRootName, HierarchyNodeType.ProjectRoot, 0);
            }

            // Group assets by their target project
            var projectGroups = assetList
                .GroupBy(a => a.TargetProject, new ProjectIdentifierComparer())
                .ToList();

            // If all assets target the same project, return a direct ProjectRoot (no SuperRoot)
            if (projectGroups.Count == 1)
            {
                var projectIdentifier = projectGroups[0].Key;
                var displayName = GetProjectDisplayName(projectIdentifier, projectName, projectNames);
                return BuildProjectSubtree(
                    projectGroups[0].ToList(),
                    displayName,
                    collectionPath,
                    expandedStates,
                    projectIdentifier,
                    depth: 0,
                    includeFolders);
            }

            // Multiple projects: create SuperRoot containing multiple ProjectRoot children
            var superRoot = new UploadHierarchyNode("super_root", "Projects", HierarchyNodeType.SuperRoot, -1);
            superRoot.IsExpanded = true; // SuperRoot is always expanded

            foreach (var group in projectGroups)
            {
                var projectIdentifier = group.Key;
                var displayName = GetProjectDisplayName(projectIdentifier, null, projectNames);
                var projectRoot = BuildProjectSubtree(
                    group.ToList(),
                    displayName,
                    collectionPath,
                    expandedStates,
                    projectIdentifier,
                    depth: 0,
                    includeFolders);

                superRoot.AddChild(projectRoot);
            }

            // Sort project roots by name
            superRoot.SortChildren();

            return superRoot;
        }

        /// <summary>
        /// Gets the display name for a project.
        /// </summary>
        static string GetProjectDisplayName(
            ProjectIdentifier projectIdentifier,
            string fallbackName,
            IReadOnlyDictionary<string, string> projectNames)
        {
            if (projectIdentifier != null && projectNames != null &&
                projectNames.TryGetValue(projectIdentifier.ProjectId, out var name))
            {
                return name;
            }

            return string.IsNullOrEmpty(fallbackName) ? "Project" : fallbackName;
        }

        /// <summary>
        /// Builds a subtree for a single project's assets.
        /// </summary>
        /// <param name="assets">The assets to organize into this project's subtree</param>
        /// <param name="projectName">Display name for the project root node</param>
        /// <param name="collectionPath">Optional collection path</param>
        /// <param name="expandedStates">Dictionary of node IDs to their expanded state</param>
        /// <param name="projectIdentifier">The project identifier</param>
        /// <param name="depth">The depth of the project root node in the tree</param>
        /// <param name="includeFolders">When true, assets are organized in folder hierarchy; when false, assets are displayed flat under project root</param>
        static UploadHierarchyNode BuildProjectSubtree(
            List<UploadAssetData> assets,
            string projectName,
            string collectionPath,
            IReadOnlyDictionary<string, bool> expandedStates,
            ProjectIdentifier projectIdentifier,
            int depth,
            bool includeFolders)
        {
            // Create project root node
            var rootId = projectIdentifier != null
                ? $"project_root_{projectIdentifier.ProjectId}"
                : (string.IsNullOrEmpty(collectionPath) ? "project_root" : NormalizePath(collectionPath));
            var rootName = string.IsNullOrEmpty(projectName) ? "Project" : projectName;
            var root = new UploadHierarchyNode(rootId, rootName, HierarchyNodeType.ProjectRoot, depth, projectIdentifier: projectIdentifier);

            // Apply expanded state to root
            if (expandedStates != null && expandedStates.TryGetValue(rootId, out var rootExpanded))
            {
                root.IsExpanded = rootExpanded;
            }

            if (assets.Count == 0)
                return root;

            // Dictionary to track folder nodes by their normalized path (scoped to this project)
            var folderNodes = new Dictionary<string, UploadHierarchyNode>(StringComparer.OrdinalIgnoreCase);

            // Build tree structure
            foreach (var assetData in assets)
            {
                var assetPath = assetData.AssetPath;
                if (string.IsNullOrEmpty(assetPath))
                    continue;

                // Determine parent node based on includeFolders flag
                UploadHierarchyNode parentNode;
                if (includeFolders)
                {
                    // Get directory path and strip the first segment (Assets/ or Packages/)
                    // e.g., "Assets/Materials/Metal/file.mat" → "Materials/Metal"
                    var directoryPath = Path.GetDirectoryName(assetPath);
                    var normalizedDir = NormalizePath(directoryPath);
                    normalizedDir = StripFirstPathSegment(normalizedDir);

                    // Get or create folder node (scoped to project in multi-project scenarios)
                    var projectIdPrefix = projectIdentifier != null ? $"{projectIdentifier.ProjectId}:" : null;
                    parentNode = GetOrCreateFolderNode(root, normalizedDir, folderNodes, expandedStates, projectIdPrefix);
                }
                else
                {
                    // Flat mode: add directly to project root
                    parentNode = root;
                }

                // Create asset node
                var assetId = assetData.Guid;
                var assetName = Path.GetFileNameWithoutExtension(assetPath);
                var assetNode = new UploadHierarchyNode(assetId, assetName, HierarchyNodeType.Asset, parentNode.Depth + 1, assetData);

                parentNode.AddChild(assetNode);
            }

            // Sort all children
            root.SortChildren();

            return root;
        }

        /// <summary>
        /// Comparer for ProjectIdentifier that treats null as a distinct group.
        /// </summary>
        class ProjectIdentifierComparer : IEqualityComparer<ProjectIdentifier>
        {
            public bool Equals(ProjectIdentifier x, ProjectIdentifier y)
            {
                if (x == null && y == null) return true;
                if (x == null || y == null) return false;
                return x.Equals(y);
            }

            public int GetHashCode(ProjectIdentifier obj)
            {
                return obj?.GetHashCode() ?? 0;
            }
        }

        /// <summary>
        /// Gets an existing folder node or creates it (and any necessary parent folders).
        /// </summary>
        /// <param name="root">The project root node</param>
        /// <param name="normalizedPath">The normalized folder path</param>
        /// <param name="folderNodes">Dictionary of existing folder nodes</param>
        /// <param name="expandedStates">Dictionary of node IDs to their expanded state</param>
        /// <param name="projectIdPrefix">Optional prefix for folder IDs to scope them per project</param>
        static UploadHierarchyNode GetOrCreateFolderNode(
            UploadHierarchyNode root,
            string normalizedPath,
            Dictionary<string, UploadHierarchyNode> folderNodes,
            IReadOnlyDictionary<string, bool> expandedStates,
            string projectIdPrefix = null)
        {
            if (string.IsNullOrEmpty(normalizedPath))
                return root;

            // Check if we already have this folder
            if (folderNodes.TryGetValue(normalizedPath, out var existingNode))
                return existingNode;

            // Need to create this folder and potentially parent folders
            var pathSegments = normalizedPath.Split('/');

            var currentParent = root;
            var currentPath = string.Empty;

            foreach (var segment in pathSegments)
            {
                if (string.IsNullOrEmpty(segment))
                    continue;

                // Build the path incrementally
                currentPath = string.IsNullOrEmpty(currentPath) ? segment : $"{currentPath}/{segment}";

                // Check if this level exists
                if (folderNodes.TryGetValue(currentPath, out var folderNode))
                {
                    currentParent = folderNode;
                    continue;
                }

                // Create new folder node with project-scoped ID to avoid state collisions in multi-project trees
                var folderId = string.IsNullOrEmpty(projectIdPrefix) ? currentPath : $"{projectIdPrefix}{currentPath}";
                var newFolder = new UploadHierarchyNode(
                    folderId,
                    segment,
                    HierarchyNodeType.Folder,
                    currentParent.Depth + 1);

                // Apply expanded state using the scoped folder ID
                if (expandedStates != null && expandedStates.TryGetValue(folderId, out var isExpanded))
                {
                    newFolder.IsExpanded = isExpanded;
                }

                currentParent.AddChild(newFolder);
                folderNodes[currentPath] = newFolder;
                currentParent = newFolder;
            }

            return currentParent;
        }

        /// <summary>
        /// Normalizes a path to use forward slashes and trim trailing slashes.
        /// </summary>
        static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            return path.Replace('\\', '/').TrimEnd('/');
        }

        /// <summary>
        /// Strips the first segment from a path.
        /// e.g., "Assets/Materials/Metal" → "Materials/Metal"
        /// e.g., "Packages/com.unity.test/Runtime" → "com.unity.test/Runtime"
        /// e.g., "Assets" → "" (empty string)
        /// </summary>
        static string StripFirstPathSegment(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            var slashIndex = path.IndexOf('/');
            return slashIndex >= 0 ? path.Substring(slashIndex + 1) : string.Empty;
        }

        /// <summary>
        /// Collects all expanded states from a tree into a dictionary.
        /// Only stores collapsed nodes (since expanded is the default).
        /// </summary>
        public static Dictionary<string, bool> CollectExpandedStates(UploadHierarchyNode root)
        {
            var states = new Dictionary<string, bool>();

            if (root == null)
                return states;

            CollectExpandedStatesRecursive(root, states);

            return states;
        }

        static void CollectExpandedStatesRecursive(UploadHierarchyNode node, Dictionary<string, bool> states)
        {
            // Only store collapsed folders (expanded is default)
            if (node.IsFolder && !node.IsExpanded)
            {
                states[node.Id] = false;
            }

            foreach (var child in node.Children)
            {
                CollectExpandedStatesRecursive(child, states);
            }
        }

        /// <summary>
        /// Applies expanded states from a dictionary to a tree.
        /// </summary>
        public static void ApplyExpandedStates(UploadHierarchyNode root, IReadOnlyDictionary<string, bool> states)
        {
            if (root == null || states == null)
                return;

            ApplyExpandedStatesRecursive(root, states);
        }

        static void ApplyExpandedStatesRecursive(UploadHierarchyNode node, IReadOnlyDictionary<string, bool> states)
        {
            if (node.IsFolder && states.TryGetValue(node.Id, out var isExpanded))
            {
                node.IsExpanded = isExpanded;
            }

            foreach (var child in node.Children)
            {
                ApplyExpandedStatesRecursive(child, states);
            }
        }

        /// <summary>
        /// Expands all folder nodes in the tree.
        /// </summary>
        public static void ExpandAll(UploadHierarchyNode root)
        {
            if (root == null)
                return;

            // SuperRoot is always expanded (not user-controllable)
            root.IsExpanded = true;

            foreach (var child in root.Children)
            {
                if (child.IsFolder)
                {
                    ExpandAll(child);
                }
            }
        }

        /// <summary>
        /// Collapses all folder nodes in the tree.
        /// SuperRoot nodes remain expanded as they are invisible containers.
        /// </summary>
        public static void CollapseAll(UploadHierarchyNode root)
        {
            if (root == null)
                return;

            // Don't collapse SuperRoot - it should always be expanded
            if (root.NodeType != HierarchyNodeType.SuperRoot)
            {
                root.IsExpanded = false;
            }

            foreach (var child in root.Children)
            {
                if (child.IsFolder)
                {
                    CollapseAll(child);
                }
            }
        }

        /// <summary>
        /// Gets statistics about the tree.
        /// </summary>
        public static (int totalAssets, int checkedAssets, int folderCount) GetTreeStats(UploadHierarchyNode root)
        {
            var totalAssets = 0;
            var checkedAssets = 0;
            var folderCount = 0;

            if (root == null)
                return (totalAssets, checkedAssets, folderCount);

            GetTreeStatsRecursive(root, ref totalAssets, ref checkedAssets, ref folderCount);

            return (totalAssets, checkedAssets, folderCount);
        }

        static void GetTreeStatsRecursive(UploadHierarchyNode node, ref int totalAssets, ref int checkedAssets, ref int folderCount)
        {
            if (node.NodeType == HierarchyNodeType.Asset)
            {
                totalAssets++;
                if (node.AssetData != null && !node.AssetData.IsIgnored)
                {
                    checkedAssets++;
                }
            }
            else if (node.NodeType != HierarchyNodeType.SuperRoot)
            {
                // Count ProjectRoot and Folder nodes, but not SuperRoot (which is an invisible container)
                folderCount++;
            }

            foreach (var child in node.Children)
            {
                GetTreeStatsRecursive(child, ref totalAssets, ref checkedAssets, ref folderCount);
            }
        }
    }
}
