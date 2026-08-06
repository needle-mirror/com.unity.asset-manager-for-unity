using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Upload.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// Virtualized tree view component for displaying upload assets in a hierarchy.
    /// </summary>
    class UploadHierarchyView : VisualElement
    {
        const int k_RowHeight = 22;
        const int k_ExtraVisibleRows = 2;
        const string k_ScrollOffsetKey = "AM4USessionData.HierarchyScrollOffset";
        const string k_ExpandedStatesKey = "AM4USessionData.HierarchyExpandedStates";

        // Static storage for expanded states (persists across domain reloads via SessionState)
        static HashSet<string> s_CollapsedNodeIds;

        static HashSet<string> CollapsedNodeIds
        {
            get
            {
                if (s_CollapsedNodeIds == null)
                {
                    var json = UnityEditor.SessionState.GetString(k_ExpandedStatesKey, "");
                    s_CollapsedNodeIds = string.IsNullOrEmpty(json)
                        ? new HashSet<string>()
                        : new HashSet<string>(json.Split('|', StringSplitOptions.RemoveEmptyEntries));
                }
                return s_CollapsedNodeIds;
            }
        }

        static void SaveExpandedStates()
        {
            var json = s_CollapsedNodeIds != null ? string.Join("|", s_CollapsedNodeIds) : "";
            UnityEditor.SessionState.SetString(k_ExpandedStatesKey, json);
        }

        readonly ScrollView m_ScrollView;
        readonly VisualElement m_RowContainer;
        readonly VisualElement m_EmptyStateContainer;
        readonly Label m_EmptyStateLabel;
        readonly List<UploadHierarchyRow> m_RowPool = new();

        UploadHierarchyNode m_RootNode;
        List<UploadHierarchyNode> m_FlattenedNodes = new();
        int m_FirstVisibleIndex;
        int m_VisibleRowCount;
        float m_LastHeight;
        readonly HashSet<string> m_SelectedNodeIds = new();
        string m_LastSelectedNodeId;
        bool m_MatchProjectStructure;

        readonly IPageManager m_PageManager;
        readonly IAssetOperationManager m_AssetOperationManager;
        readonly IUploadManager m_UploadManager;
        readonly IUnityConnectProxy m_UnityConnectProxy;
        readonly IAssetDataManager m_AssetDataManager;
        readonly IPermissionsManager m_PermissionsManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;

        /// <summary>
        /// Maps ProjectIdentifier to role text for multi-project scenarios.
        /// </summary>
        readonly Dictionary<string, string> m_ProjectRoles = new();

        float ScrollOffset
        {
            get => UnityEditor.SessionState.GetFloat(k_ScrollOffsetKey, 0f);
            set => UnityEditor.SessionState.SetFloat(k_ScrollOffsetKey, value);
        }

        /// <summary>
        /// Event fired when a node's toggle state changes.
        /// </summary>
        public event Action<UploadHierarchyNode, bool> NodeToggled;

        /// <summary>
        /// Event fired when a folder node's expand state changes.
        /// </summary>
        public event Action<UploadHierarchyNode> NodeExpandToggled;

        /// <summary>
        /// Event fired when node selection changes.
        /// Passes the clicked node and whether it's an additive selection.
        /// </summary>
        public event Action<UploadHierarchyNode, bool> NodeSelected;

        /// <summary>
        /// Event fired when a range of nodes is selected (Shift+click).
        /// Passes the list of nodes in the range.
        /// </summary>
        public event Action<IReadOnlyList<UploadHierarchyNode>> NodesRangeSelected;

        public UploadHierarchyView(
            IPageManager pageManager,
            IAssetOperationManager assetOperationManager,
            IUploadManager uploadManager,
            IUnityConnectProxy unityConnectProxy,
            IAssetDataManager assetDataManager,
            IPermissionsManager permissionsManager,
            IProjectOrganizationProvider projectOrganizationProvider)
        {
            m_PageManager = pageManager;
            m_AssetOperationManager = assetOperationManager;
            m_UploadManager = uploadManager;
            m_UnityConnectProxy = unityConnectProxy;
            m_AssetDataManager = assetDataManager;
            m_PermissionsManager = permissionsManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;

            AddToClassList("upload-hierarchy-view");

            m_ScrollView = new ScrollView(ScrollViewMode.Vertical);
            m_ScrollView.AddToClassList("upload-hierarchy-scrollview");
            m_ScrollView.verticalScroller.valueChanged += OnScroll;
            Add(m_ScrollView);

            m_RowContainer = new VisualElement();
            m_RowContainer.AddToClassList("upload-hierarchy-row-container");
            m_ScrollView.Add(m_RowContainer);

            // Empty state display
            m_EmptyStateContainer = new VisualElement();
            m_EmptyStateContainer.AddToClassList("upload-hierarchy-empty-state");
            m_EmptyStateLabel = new Label();
            m_EmptyStateLabel.AddToClassList("upload-hierarchy-empty-state__label");
            m_EmptyStateContainer.Add(m_EmptyStateLabel);
            Add(m_EmptyStateContainer);
            UIElementsUtils.Hide(m_EmptyStateContainer);

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            m_ScrollView.RegisterCallback<GeometryChangedEvent>(OnScrollViewGeometryChanged);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            m_ScrollView.contentContainer.usageHints &= ~UsageHints.GroupTransform;
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_UploadManager.UploadBegan += OnUploadBegan;
            m_UploadManager.UploadEnded += OnUploadEnded;
            m_ProjectOrganizationProvider.ProjectSelectionChanged += OnProjectSelectionChanged;
            TaskUtils.TrackException(RefreshRolesAsync());
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_UploadManager.UploadBegan -= OnUploadBegan;
            m_UploadManager.UploadEnded -= OnUploadEnded;
            m_ProjectOrganizationProvider.ProjectSelectionChanged -= OnProjectSelectionChanged;
            SaveScrollOffset();
        }

        async void OnProjectSelectionChanged(ProjectOrLibraryInfo projectOrLibraryInfo, CollectionInfo _)
        {
            await RefreshRolesAsync();
            RebindVisibleRows();
        }

        /// <summary>
        /// Refreshes roles for all project nodes in the hierarchy.
        /// </summary>
        async System.Threading.Tasks.Task RefreshRolesAsync()
        {
            m_ProjectRoles.Clear();

            if (m_RootNode == null || m_PermissionsManager == null)
                return;

            // Collect all ProjectRoot nodes
            var projectNodes = CollectProjectNodes(m_RootNode);

            foreach (var node in projectNodes)
            {
                try
                {
                    var projectIdentifier = node.ProjectIdentifier;
                    if (projectIdentifier == null ||
                        string.IsNullOrEmpty(projectIdentifier.OrganizationId) ||
                        string.IsNullOrEmpty(projectIdentifier.ProjectId))
                    {
                        // Fall back to selected project for legacy compatibility
                        var projectOrLibrary = m_ProjectOrganizationProvider?.SelectedProjectOrLibrary;
                        if (projectOrLibrary == null || projectOrLibrary.IsAssetLibrary)
                            continue;

                        var organizationId = m_ProjectOrganizationProvider?.SelectedOrganization?.Id;
                        if (string.IsNullOrEmpty(organizationId) || string.IsNullOrEmpty(projectOrLibrary.Id))
                            continue;

                        var role = await m_PermissionsManager.GetRoleAsync(organizationId, projectOrLibrary.Id);
                        var roleText = role != Role.None ? role.ToString() : string.Empty;
                        m_ProjectRoles[node.Id] = roleText;
                    }
                    else
                    {
                        var role = await m_PermissionsManager.GetRoleAsync(projectIdentifier.OrganizationId, projectIdentifier.ProjectId);
                        var roleText = role != Role.None ? role.ToString() : string.Empty;
                        m_ProjectRoles[node.Id] = roleText;
                    }
                }
                catch (Exception)
                {
                    // If we can't get the role for this project, just skip it
                    // The role chip won't be displayed for this project
                }
            }
        }

        /// <summary>
        /// Collects all ProjectRoot nodes from the tree.
        /// </summary>
        static List<UploadHierarchyNode> CollectProjectNodes(UploadHierarchyNode root)
        {
            var projectNodes = new List<UploadHierarchyNode>();

            if (root.NodeType == HierarchyNodeType.SuperRoot)
            {
                // SuperRoot contains multiple ProjectRoot children
                foreach (var child in root.Children)
                {
                    if (child.NodeType == HierarchyNodeType.ProjectRoot)
                    {
                        projectNodes.Add(child);
                    }
                }
            }
            else if (root.NodeType == HierarchyNodeType.ProjectRoot)
            {
                // Single project root
                projectNodes.Add(root);
            }

            return projectNodes;
        }

        /// <summary>
        /// Gets the role text for a given node.
        /// </summary>
        string GetRoleTextForNode(UploadHierarchyNode node)
        {
            if (node == null || node.NodeType != HierarchyNodeType.ProjectRoot)
                return string.Empty;

            return m_ProjectRoles.TryGetValue(node.Id, out var roleText) ? roleText : string.Empty;
        }

        void OnUploadBegan()
        {
            SetEnabled(false);
            foreach (var row in m_RowPool)
            {
                row.SetToggleEnabled(false);
            }
        }

        void OnUploadEnded(UploadEndedStatus status)
        {
            SetEnabled(true);
            foreach (var row in m_RowPool)
            {
                row.SetToggleEnabled(true);
            }
        }

        /// <summary>
        /// Sets the root node and rebuilds the view.
        /// </summary>
        public void SetData(UploadHierarchyNode root)
        {
            m_RootNode = root;
            RebuildFlattenedList();
            TaskUtils.TrackException(RefreshRolesAsync());
            Refresh();
        }

        /// <summary>
        /// Sets whether Match Project Structure mode is enabled.
        /// This affects the display of re-upload collection warnings.
        /// </summary>
        public void SetMatchProjectStructure(bool matchProjectStructure)
        {
            if (m_MatchProjectStructure == matchProjectStructure)
                return;

            m_MatchProjectStructure = matchProjectStructure;
            RebindVisibleRows();
        }

        /// <summary>
        /// Refreshes the view with the current data.
        /// </summary>
        public void Refresh()
        {
            if (m_RootNode == null || float.IsNaN(m_ScrollView.layout.height) || m_ScrollView.layout.height <= 0)
                return;

            m_LastHeight = m_ScrollView.layout.height;
            RefreshRows();
        }

        /// <summary>
        /// Clears all data from the view.
        /// </summary>
        public new void Clear()
        {
            m_RootNode = null;
            m_FlattenedNodes.Clear();
            ClearRows();
        }

        /// <summary>
        /// Shows the empty state message.
        /// </summary>
        public void ShowEmptyState(string message)
        {
            m_EmptyStateLabel.text = message;
            UIElementsUtils.Show(m_EmptyStateContainer);
            UIElementsUtils.Hide(m_ScrollView);
        }

        /// <summary>
        /// Hides the empty state message.
        /// </summary>
        public void HideEmptyState()
        {
            UIElementsUtils.Hide(m_EmptyStateContainer);
            UIElementsUtils.Show(m_ScrollView);
        }

        /// <summary>
        /// Expands all nodes in the tree.
        /// </summary>
        public void ExpandAll()
        {
            if (m_RootNode == null)
                return;

            UploadHierarchyBuilder.ExpandAll(m_RootNode);
            RebuildFlattenedList();
            Refresh();
        }

        /// <summary>
        /// Collapses all nodes in the tree.
        /// </summary>
        public void CollapseAll()
        {
            if (m_RootNode == null)
                return;

            UploadHierarchyBuilder.CollapseAll(m_RootNode);
            RebuildFlattenedList();
            Refresh();
        }

        void RebuildFlattenedList()
        {
            m_FlattenedNodes.Clear();

            if (m_RootNode == null)
                return;

            if (m_RootNode.NodeType == HierarchyNodeType.SuperRoot)
            {
                // SuperRoot is invisible - only include its children's visible nodes
                foreach (var child in m_RootNode.Children)
                {
                    m_FlattenedNodes.AddRange(child.GetFlattenedVisibleNodes());
                }
            }
            else
            {
                m_FlattenedNodes = m_RootNode.GetFlattenedVisibleNodes().ToList();
            }
        }

        void OnGeometryChanged(GeometryChangedEvent evt)
        {
            if (Mathf.Approximately(evt.newRect.height, evt.oldRect.height))
                return;

            if (evt.newRect.height <= 0)
                return;

            m_LastHeight = evt.newRect.height;
            RefreshRows();
        }

        void OnScrollViewGeometryChanged(GeometryChangedEvent evt)
        {
            // When scroll view becomes visible (height changes from 0 to positive), refresh
            if (evt.oldRect.height <= 0 && evt.newRect.height > 0)
            {
                m_LastHeight = evt.newRect.height;
                RefreshRows();
            }
        }

        void OnScroll(float offset)
        {
            if (m_FlattenedNodes.Count == 0)
                return;

            var contentHeight = m_FlattenedNodes.Count * k_RowHeight;
            var viewportHeight = m_ScrollView.contentViewport.resolvedStyle.height;

            var firstVisibleIndex = Mathf.FloorToInt(offset / k_RowHeight);
            firstVisibleIndex = Mathf.Max(0, Mathf.Min(firstVisibleIndex, m_FlattenedNodes.Count - 1));

            m_RowContainer.style.paddingTop = firstVisibleIndex * k_RowHeight;

            if (firstVisibleIndex != m_FirstVisibleIndex)
            {
                m_FirstVisibleIndex = firstVisibleIndex;
                RebindVisibleRows();
            }
        }

        void RefreshRows()
        {
            if (m_FlattenedNodes.Count == 0)
            {
                ClearRows();
                return;
            }

            var contentHeight = m_FlattenedNodes.Count * k_RowHeight;
            m_RowContainer.style.height = contentHeight;

            var visibleRowCount = Mathf.CeilToInt(m_LastHeight / k_RowHeight) + k_ExtraVisibleRows;
            visibleRowCount = Mathf.Min(visibleRowCount, m_FlattenedNodes.Count);

            // Adjust pool size
            while (m_RowPool.Count < visibleRowCount)
            {
                var row = CreateRow();
                m_RowPool.Add(row);
                m_RowContainer.Add(row);
            }

            while (m_RowPool.Count > visibleRowCount)
            {
                var lastRow = m_RowPool[^1];
                lastRow.Unbind();
                m_RowContainer.Remove(lastRow);
                m_RowPool.RemoveAt(m_RowPool.Count - 1);
            }

            m_VisibleRowCount = visibleRowCount;

            // Recalculate first visible from current scroll offset
            var currentOffset = m_ScrollView.verticalScroller.value;
            m_FirstVisibleIndex = Mathf.FloorToInt(currentOffset / k_RowHeight);
            m_FirstVisibleIndex = Mathf.Max(0, Mathf.Min(m_FirstVisibleIndex, m_FlattenedNodes.Count - m_VisibleRowCount));

            m_RowContainer.style.paddingTop = m_FirstVisibleIndex * k_RowHeight;

            RebindVisibleRows();
        }

        void ClearRows()
        {
            foreach (var row in m_RowPool)
            {
                row.Unbind();
            }
            m_RowPool.Clear();
            m_RowContainer.Clear();
            m_VisibleRowCount = 0;
            m_FirstVisibleIndex = 0;
            m_ScrollView.scrollOffset = Vector2.zero;
            m_RowContainer.style.paddingTop = 0;
            m_RowContainer.style.height = 0;
        }

        void RebindVisibleRows()
        {
            for (var i = 0; i < m_RowPool.Count; i++)
            {
                var dataIndex = m_FirstVisibleIndex + i;
                var row = m_RowPool[i];

                if (dataIndex < m_FlattenedNodes.Count)
                {
                    var node = m_FlattenedNodes[dataIndex];
                    var roleText = GetRoleTextForNode(node);
                    row.Bind(node, roleText, m_MatchProjectStructure);
                    row.SetSelected(m_SelectedNodeIds.Contains(node.Id));
                }
                else
                {
                    row.Unbind();
                    row.SetSelected(false);
                }
            }
        }

        void RefreshSelectionState()
        {
            foreach (var row in m_RowPool)
            {
                var node = row.BoundNode;
                row.SetSelected(node != null && m_SelectedNodeIds.Contains(node.Id));
            }
        }

        /// <summary>
        /// Selects a node by ID and updates the visual state.
        /// </summary>
        public void SelectNode(string nodeId)
        {
            m_SelectedNodeIds.Clear();
            if (!string.IsNullOrEmpty(nodeId))
            {
                m_SelectedNodeIds.Add(nodeId);
                m_LastSelectedNodeId = nodeId;
            }
            RefreshSelectionState();
        }

        /// <summary>
        /// Selects multiple nodes by their IDs and updates the visual state.
        /// </summary>
        public void SelectNodes(IEnumerable<string> nodeIds)
        {
            m_SelectedNodeIds.Clear();
            foreach (var nodeId in nodeIds)
            {
                if (!string.IsNullOrEmpty(nodeId))
                {
                    m_SelectedNodeIds.Add(nodeId);
                }
            }
            if (m_SelectedNodeIds.Count > 0)
            {
                m_LastSelectedNodeId = m_SelectedNodeIds.Last();
            }
            RefreshSelectionState();
        }

        /// <summary>
        /// Clears all selected nodes.
        /// </summary>
        public void ClearSelection()
        {
            m_SelectedNodeIds.Clear();
            m_LastSelectedNodeId = null;
            RefreshSelectionState();
        }

        UploadHierarchyRow CreateRow()
        {
            var row = new UploadHierarchyRow(m_UnityConnectProxy, m_AssetOperationManager, m_AssetDataManager);

            row.ToggleClicked += OnRowToggleClicked;
            row.ExpandClicked += OnRowExpandClicked;
            row.RowClicked += OnRowClicked;

            return row;
        }

        void OnRowToggleClicked(UploadHierarchyNode node, bool newState)
        {
            NodeToggled?.Invoke(node, newState);
        }

        void OnRowExpandClicked(UploadHierarchyNode node)
        {
            node.IsExpanded = !node.IsExpanded;

            // Persist expanded state (store collapsed nodes only)
            SetNodeExpandedState(node.Id, node.IsExpanded);

            NodeExpandToggled?.Invoke(node);

            RebuildFlattenedList();
            Refresh();
        }

        /// <summary>
        /// Sets the expanded state for a node and persists it.
        /// </summary>
        public static void SetNodeExpandedState(string nodeId, bool isExpanded)
        {
            if (string.IsNullOrEmpty(nodeId))
                return;

            if (isExpanded)
            {
                CollapsedNodeIds.Remove(nodeId);
            }
            else
            {
                CollapsedNodeIds.Add(nodeId);
            }
            SaveExpandedStates();
        }

        /// <summary>
        /// Gets the expanded state for a node.
        /// </summary>
        public static bool GetNodeExpandedState(string nodeId, bool defaultValue = true)
        {
            if (string.IsNullOrEmpty(nodeId))
                return defaultValue;

            // If in collapsed set, return false; otherwise return default (expanded)
            return !CollapsedNodeIds.Contains(nodeId);
        }

        /// <summary>
        /// Gets all expanded states as a dictionary (for compatibility with UploadHierarchyBuilder).
        /// </summary>
        public static IReadOnlyDictionary<string, bool> GetAllExpandedStates()
        {
            var result = new Dictionary<string, bool>();
            foreach (var nodeId in CollapsedNodeIds)
            {
                result[nodeId] = false;
            }
            return result;
        }

        /// <summary>
        /// Clears all persisted expanded states.
        /// </summary>
        public static void ClearExpandedStates()
        {
            s_CollapsedNodeIds?.Clear();
            SaveExpandedStates();
        }

        void OnRowClicked(UploadHierarchyNode node, EventModifiers modifiers)
        {
            if (node == null)
                return;

            var isAdditive = IsAdditiveSelection(modifiers);
            var isContinuous = IsContinuousSelection(modifiers);

            if (isContinuous && !string.IsNullOrEmpty(m_LastSelectedNodeId) && m_SelectedNodeIds.Count > 0)
            {
                // Shift+click: select range from last selected to current
                var lastIndex = m_FlattenedNodes.FindIndex(n => n.Id == m_LastSelectedNodeId);
                var currentIndex = m_FlattenedNodes.FindIndex(n => n.Id == node.Id);

                if (lastIndex >= 0 && currentIndex >= 0)
                {
                    var startIndex = Math.Min(lastIndex, currentIndex);
                    var endIndex = Math.Max(lastIndex, currentIndex);

                    // Collect all asset nodes in the range
                    var rangeNodes = new List<UploadHierarchyNode>();
                    for (var i = startIndex; i <= endIndex; i++)
                    {
                        var rangeNode = m_FlattenedNodes[i];
                        if (rangeNode.NodeType == HierarchyNodeType.Asset)
                        {
                            m_SelectedNodeIds.Add(rangeNode.Id);
                            rangeNodes.Add(rangeNode);
                        }
                    }

                    RefreshSelectionState();

                    // Fire event with the range of nodes
                    NodesRangeSelected?.Invoke(rangeNodes);
                    return;
                }
            }

            if (isAdditive)
            {
                // Ctrl/Cmd+click: toggle selection of this node
                if (m_SelectedNodeIds.Contains(node.Id))
                {
                    m_SelectedNodeIds.Remove(node.Id);
                }
                else
                {
                    m_SelectedNodeIds.Add(node.Id);
                    m_LastSelectedNodeId = node.Id;
                }
            }
            else
            {
                // Regular click: clear selection and select only this node
                m_SelectedNodeIds.Clear();
                m_SelectedNodeIds.Add(node.Id);
                m_LastSelectedNodeId = node.Id;
            }

            RefreshSelectionState();
            NodeSelected?.Invoke(node, isAdditive);
        }

        static bool IsContinuousSelection(EventModifiers modifiers)
        {
#if UNITY_EDITOR_OSX
            return (modifiers & EventModifiers.Shift) != 0 && (modifiers & EventModifiers.Command) == 0;
#else
            return (modifiers & EventModifiers.Shift) != 0;
#endif
        }

        static bool IsAdditiveSelection(EventModifiers modifiers)
        {
#if UNITY_EDITOR_OSX
            var additiveModifier = EventModifiers.Command;
#else
            var additiveModifier = EventModifiers.Control;
#endif
            return (modifiers & additiveModifier) != 0 && (modifiers & EventModifiers.Shift) == 0;
        }

        void SaveScrollOffset()
        {
            ScrollOffset = m_ScrollView.verticalScroller.value;
        }

        /// <summary>
        /// Restores the scroll position from session state.
        /// </summary>
        public void RestoreScrollOffset()
        {
            m_ScrollView.verticalScroller.value = ScrollOffset;
        }

        /// <summary>
        /// Scrolls to make a specific node visible.
        /// </summary>
        public void ScrollToNode(UploadHierarchyNode node)
        {
            if (node == null)
                return;

            var index = m_FlattenedNodes.IndexOf(node);
            if (index < 0)
                return;

            var targetOffset = index * k_RowHeight;
            m_ScrollView.verticalScroller.value = targetOffset;
        }
    }
}
