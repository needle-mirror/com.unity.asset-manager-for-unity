using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AssetManager.Core.Editor;
using UnityEngine;

namespace Unity.AssetManager.Upload.Editor
{
    /// <summary>
    /// Represents the type of node in the upload hierarchy.
    /// </summary>
    enum HierarchyNodeType
    {
        /// <summary>Invisible super-root containing multiple project roots (for multi-project uploads).</summary>
        SuperRoot,
        /// <summary>Root node representing a project.</summary>
        ProjectRoot,
        /// <summary>Folder node representing a collection or directory.</summary>
        Folder,
        /// <summary>Leaf node representing an uploadable asset.</summary>
        Asset
    }

    /// <summary>
    /// Represents the tri-state checkbox state for hierarchy nodes.
    /// </summary>
    enum CheckState
    {
        /// <summary>All children are unchecked (ignored).</summary>
        Unchecked,
        /// <summary>All children are checked (included).</summary>
        Checked,
        /// <summary>Some children are checked and some are unchecked.</summary>
        Indeterminate
    }

    /// <summary>
    /// View model for a node in the upload hierarchy tree.
    /// Represents either a folder (ProjectRoot, Folder) or an asset leaf node.
    /// </summary>
    [Serializable]
    class UploadHierarchyNode
    {
        [SerializeField]
        string m_Id;

        [SerializeField]
        string m_Name;

        [SerializeField]
        HierarchyNodeType m_NodeType;

        [SerializeField]
        int m_Depth;

        [SerializeField]
        bool m_IsExpanded = true;

        [SerializeReference]
        UploadAssetData m_AssetData;

        [SerializeField]
        ProjectIdentifier m_ProjectIdentifier;

        [SerializeReference]
        List<UploadHierarchyNode> m_Children = new();

        UploadHierarchyNode m_Parent;

        /// <summary>
        /// Unique identifier for this node.
        /// For assets: the asset GUID.
        /// For folders: the normalized folder path.
        /// </summary>
        public string Id => m_Id;

        /// <summary>
        /// Display name for this node.
        /// </summary>
        public string Name => m_Name;

        /// <summary>
        /// The type of this node (ProjectRoot, Folder, or Asset).
        /// </summary>
        public HierarchyNodeType NodeType => m_NodeType;

        /// <summary>
        /// The asset data for leaf nodes. Null for folder nodes.
        /// </summary>
        public UploadAssetData AssetData => m_AssetData;

        /// <summary>
        /// The project identifier for ProjectRoot nodes. Used for role lookup in multi-project scenarios.
        /// </summary>
        public ProjectIdentifier ProjectIdentifier => m_ProjectIdentifier;

        /// <summary>
        /// Child nodes of this folder. Empty for asset nodes.
        /// </summary>
        public IReadOnlyList<UploadHierarchyNode> Children => m_Children;

        /// <summary>
        /// Parent node. Null for root.
        /// </summary>
        public UploadHierarchyNode Parent
        {
            get => m_Parent;
            internal set => m_Parent = value;
        }

        /// <summary>
        /// Depth in the tree (0 for root).
        /// </summary>
        public int Depth => m_Depth;

        /// <summary>
        /// Whether this folder node is expanded in the UI.
        /// </summary>
        public bool IsExpanded
        {
            get => m_IsExpanded;
            set => m_IsExpanded = value;
        }

        /// <summary>
        /// Whether this node is a folder-like node (can have children).
        /// SuperRoot, ProjectRoot, and Folder types return true.
        /// </summary>
        public bool IsFolder => m_NodeType != HierarchyNodeType.Asset;

        /// <summary>
        /// Whether this node has any children.
        /// </summary>
        public bool HasChildren => m_Children.Count > 0;

        /// <summary>
        /// File extension for asset nodes (e.g., ".prefab"). Empty for folders.
        /// </summary>
        public string Extension => m_AssetData?.PrimaryExtension ?? string.Empty;

        /// <summary>
        /// Whether this node has any descendant assets that can be toggled (CanBeIgnored == true).
        /// For asset nodes, returns whether this asset can be ignored.
        /// For folder nodes, returns whether any descendant asset can be ignored.
        /// </summary>
        public bool HasToggleableDescendants
        {
            get
            {
                if (m_NodeType == HierarchyNodeType.Asset)
                {
                    return m_AssetData?.CanBeIgnored ?? false;
                }

                return GetAllDescendantAssets().Any(a => a.AssetData?.CanBeIgnored == true);
            }
        }

        /// <summary>
        /// Whether this node is visible in the hierarchy based on:
        /// - For assets: always visible (visibility filtering happens at tree build time)
        /// - For folders: has at least one visible child
        /// </summary>
        public bool IsVisible
        {
            get
            {
                if (m_NodeType == HierarchyNodeType.Asset)
                    return true;

                // Folder is visible if it has at least one visible child
                return m_Children.Any(c => c.IsVisible);
            }
        }

        /// <summary>
        /// Computed tri-state check state for this node.
        /// - For assets: Checked if not ignored, Unchecked if ignored
        /// - For folders: computed from children states
        /// </summary>
        public CheckState CheckState
        {
            get
            {
                if (m_NodeType == HierarchyNodeType.Asset)
                {
                    if (m_AssetData == null)
                        return CheckState.Unchecked;

                    return m_AssetData.IsIgnored ? CheckState.Unchecked : CheckState.Checked;
                }

                return ComputeFolderCheckState();
            }
        }

        /// <summary>
        /// Creates a new hierarchy node.
        /// </summary>
        /// <param name="id">Unique identifier (GUID for assets, path for folders)</param>
        /// <param name="name">Display name</param>
        /// <param name="nodeType">Type of node</param>
        /// <param name="depth">Tree depth (0 for root)</param>
        /// <param name="assetData">Asset data for leaf nodes (null for folders)</param>
        /// <param name="projectIdentifier">Project identifier for ProjectRoot nodes (null for other node types)</param>
        public UploadHierarchyNode(string id, string name, HierarchyNodeType nodeType, int depth, UploadAssetData assetData = null, ProjectIdentifier projectIdentifier = null)
        {
            m_Id = id;
            m_Name = name;
            m_NodeType = nodeType;
            m_Depth = depth;
            m_AssetData = assetData;
            m_ProjectIdentifier = projectIdentifier;
        }

        /// <summary>
        /// Adds a child node to this folder.
        /// </summary>
        public void AddChild(UploadHierarchyNode child)
        {
            if (m_NodeType == HierarchyNodeType.Asset)
            {
                Debug.LogError("Cannot add children to an asset node.");
                return;
            }

            child.m_Parent = this;
            m_Children.Add(child);
        }

        /// <summary>
        /// Removes a child node from this folder.
        /// </summary>
        public bool RemoveChild(UploadHierarchyNode child)
        {
            if (m_Children.Remove(child))
            {
                child.m_Parent = null;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Clears all children from this folder.
        /// </summary>
        public void ClearChildren()
        {
            foreach (var child in m_Children)
            {
                child.m_Parent = null;
            }
            m_Children.Clear();
        }

        /// <summary>
        /// Gets all descendant asset nodes recursively.
        /// </summary>
        public IEnumerable<UploadHierarchyNode> GetAllDescendantAssets()
        {
            foreach (var child in m_Children)
            {
                if (child.NodeType == HierarchyNodeType.Asset)
                {
                    yield return child;
                }
                else
                {
                    foreach (var descendant in child.GetAllDescendantAssets())
                    {
                        yield return descendant;
                    }
                }
            }
        }

        /// <summary>
        /// Gets all visible nodes in tree order (pre-order traversal), respecting expand state.
        /// </summary>
        public IEnumerable<UploadHierarchyNode> GetFlattenedVisibleNodes()
        {
            if (!IsVisible)
                yield break;

            yield return this;

            if (IsFolder && IsExpanded)
            {
                foreach (var child in m_Children)
                {
                    foreach (var node in child.GetFlattenedVisibleNodes())
                    {
                        yield return node;
                    }
                }
            }
        }

        /// <summary>
        /// Finds a node by ID in this subtree.
        /// </summary>
        public UploadHierarchyNode FindById(string id)
        {
            if (m_Id == id)
                return this;

            foreach (var child in m_Children)
            {
                var found = child.FindById(id);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// Sorts children recursively by name, with folders first.
        /// For SuperRoot nodes, sorts ProjectRoot children by name.
        /// </summary>
        public void SortChildren()
        {
            if (m_NodeType == HierarchyNodeType.SuperRoot)
            {
                // Sort ProjectRoot children by name
                m_Children = m_Children
                    .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                m_Children = m_Children
                    .OrderBy(c => c.NodeType == HierarchyNodeType.Asset ? 1 : 0)
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            foreach (var child in m_Children)
            {
                if (child.IsFolder)
                {
                    child.SortChildren();
                }
            }
        }

        CheckState ComputeFolderCheckState()
        {
            var visibleChildren = m_Children.Where(c => c.IsVisible).ToList();

            if (visibleChildren.Count == 0)
                return CheckState.Unchecked;

            var childStates = visibleChildren.Select(c => c.CheckState).Distinct().ToList();

            if (childStates.Count == 1)
            {
                return childStates[0];
            }

            // Mixed states (or contains Indeterminate) = Indeterminate
            return CheckState.Indeterminate;
        }

        public override string ToString()
        {
            return $"{m_NodeType}: {m_Name} (Depth={m_Depth}, Children={m_Children.Count})";
        }
    }
}
