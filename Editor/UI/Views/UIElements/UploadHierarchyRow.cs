using System;
using System.IO;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Upload.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.UI.Editor
{
    /// <summary>
    /// A single row in the upload hierarchy view, representing either a folder or an asset.
    /// </summary>
    class UploadHierarchyRow : VisualElement
    {
        const int k_IndentWidth = 16;
        const int k_ThumbnailSize = 20;

        static class UssStyles
        {
            public const string Row = "upload-hierarchy-row";
            public const string RowSelected = "upload-hierarchy-row--selected";
            public const string RowHovered = "upload-hierarchy-row--hovered";
            public const string RowIgnored = "upload-hierarchy-row--ignored";
            public const string RowFolder = "upload-hierarchy-row--folder";
            public const string RowAsset = "upload-hierarchy-row--asset";
            public const string RowProjectRoot = "upload-hierarchy-row--project-root";
            public const string RowSuperRoot = "upload-hierarchy-row--super-root";
            public const string IndentSpacer = "upload-hierarchy-row__indent";
            public const string ExpandArrow = "upload-hierarchy-row__expand-arrow";
            public const string ExpandArrowExpanded = "upload-hierarchy-row__expand-arrow--expanded";
            public const string ExpandArrowCollapsed = "upload-hierarchy-row__expand-arrow--collapsed";
            public const string ExpandArrowHidden = "upload-hierarchy-row__expand-arrow--hidden";
            public const string Checkbox = "upload-hierarchy-row__checkbox";
            public const string CheckboxIndeterminate = "upload-hierarchy-row__checkbox--indeterminate";
            public const string ThumbnailContainer = "upload-hierarchy-row__thumbnail-container";
            public const string Thumbnail = "upload-hierarchy-row__thumbnail";
            public const string ThumbnailFolder = "upload-hierarchy-row__thumbnail--folder";
            public const string ThumbnailFolderCollapsed = "upload-hierarchy-row__thumbnail--folder-collapsed";
            public const string ThumbnailProjectRoot = "upload-hierarchy-row__thumbnail--project-root";
            public const string StatusOverlay = "upload-hierarchy-row__status-overlay";
            public const string NameLabel = "upload-hierarchy-row__name";
            public const string ExtensionLabel = "upload-hierarchy-row__extension";
            public const string DependencyIcon = "upload-hierarchy-row__dependency-icon";
            public const string ReuploadCollectionWarningIcon = "upload-hierarchy-row__reupload-collection-warning-icon";
            public const string RoleChip = "upload-hierarchy-row__role-chip";
            public const string ProgressBar = "upload-hierarchy-row__progress-bar";
            public const string Spacer = "upload-hierarchy-row__spacer";
        }

        readonly VisualElement m_IndentSpacer;
        readonly VisualElement m_ExpandArrow;
        readonly Toggle m_Checkbox;
        readonly VisualElement m_ThumbnailContainer;
        readonly VisualElement m_Thumbnail;
        readonly VisualElement m_StatusOverlay;
        readonly Label m_NameLabel;
        readonly Label m_ExtensionLabel;
        readonly VisualElement m_DependencyIcon;
        readonly Label m_ReuploadCollectionWarningIcon;
        readonly Label m_RoleChip;
        readonly OperationProgressBar m_ProgressBar;
        readonly VisualElement m_Spacer;

        readonly IUnityConnectProxy m_UnityConnectProxy;
        readonly IAssetOperationManager m_AssetOperationManager;
        readonly IAssetDataManager m_AssetDataManager;

        UploadHierarchyNode m_BoundNode;
        bool m_IsSelected;
        bool m_MatchProjectStructure;

        AssetContextMenu m_ContextMenu;
        ContextualMenuManipulator m_ContextualMenuManipulator;

        /// <summary>
        /// Event fired when the toggle checkbox is clicked.
        /// </summary>
        public event Action<UploadHierarchyNode, bool> ToggleClicked;

        /// <summary>
        /// Event fired when the expand arrow is clicked.
        /// </summary>
        public event Action<UploadHierarchyNode> ExpandClicked;

        /// <summary>
        /// Event fired when the row is clicked (for selection).
        /// Passes the node and the event modifiers for multi-selection support.
        /// </summary>
        public event Action<UploadHierarchyNode, EventModifiers> RowClicked;

        /// <summary>
        /// Gets the currently bound node.
        /// </summary>
        public UploadHierarchyNode BoundNode => m_BoundNode;

        public UploadHierarchyRow(
            IUnityConnectProxy unityConnectProxy,
            IAssetOperationManager assetOperationManager,
            IAssetDataManager assetDataManager)
        {
            m_UnityConnectProxy = unityConnectProxy;
            m_AssetOperationManager = assetOperationManager;
            m_AssetDataManager = assetDataManager;

            AddToClassList(UssStyles.Row);

            // Indent spacer
            m_IndentSpacer = new VisualElement();
            m_IndentSpacer.AddToClassList(UssStyles.IndentSpacer);
            Add(m_IndentSpacer);

            // Expand arrow
            m_ExpandArrow = new VisualElement();
            m_ExpandArrow.AddToClassList(UssStyles.ExpandArrow);
            m_ExpandArrow.RegisterCallback<ClickEvent>(OnExpandArrowClicked);
            Add(m_ExpandArrow);

            // Checkbox (tri-state toggle)
            m_Checkbox = new Toggle();
            m_Checkbox.AddToClassList(UssStyles.Checkbox);
            m_Checkbox.RegisterValueChangedCallback(OnCheckboxValueChanged);
            Add(m_Checkbox);

            // Thumbnail container (for positioning status overlay relative to thumbnail)
            m_ThumbnailContainer = new VisualElement();
            m_ThumbnailContainer.AddToClassList(UssStyles.ThumbnailContainer);
            Add(m_ThumbnailContainer);

            // Thumbnail
            m_Thumbnail = new VisualElement();
            m_Thumbnail.AddToClassList(UssStyles.Thumbnail);
            m_ThumbnailContainer.Add(m_Thumbnail);

            // Status overlay (for upload status icons)
            m_StatusOverlay = new VisualElement();
            m_StatusOverlay.AddToClassList(UssStyles.StatusOverlay);
            m_StatusOverlay.pickingMode = PickingMode.Ignore;
            m_ThumbnailContainer.Add(m_StatusOverlay);

            // Name label
            m_NameLabel = new Label();
            m_NameLabel.AddToClassList(UssStyles.NameLabel);
            Add(m_NameLabel);

            // Extension label (muted, for assets only)
            m_ExtensionLabel = new Label();
            m_ExtensionLabel.AddToClassList(UssStyles.ExtensionLabel);
            Add(m_ExtensionLabel);

            // Dependency icon (shown inline after name for linked assets)
            m_DependencyIcon = new VisualElement();
            m_DependencyIcon.AddToClassList(UssStyles.DependencyIcon);
            Add(m_DependencyIcon);

            // Role chip (for project root only)
            m_RoleChip = new Label();
            m_RoleChip.AddToClassList(UssStyles.RoleChip);
            m_RoleChip.pickingMode = PickingMode.Ignore;
            Add(m_RoleChip);

            // Progress bar (for upload operations)
            m_ProgressBar = new OperationProgressBar { pickingMode = PickingMode.Ignore };
            m_ProgressBar.AddToClassList(UssStyles.ProgressBar);
            Add(m_ProgressBar);

            // Spacer to fill remaining space
            m_Spacer = new VisualElement();
            m_Spacer.AddToClassList(UssStyles.Spacer);
            Add(m_Spacer);

            // Re-upload collection warning icon (shown at far right when Match Project Structure is enabled and asset is already in a collection)
            m_ReuploadCollectionWarningIcon = new Label("i");
            m_ReuploadCollectionWarningIcon.AddToClassList(UssStyles.ReuploadCollectionWarningIcon);
            Add(m_ReuploadCollectionWarningIcon);

            // Row click handler
            RegisterCallback<ClickEvent>(OnRowClicked);
            RegisterCallback<MouseEnterEvent>(OnMouseEnter);
            RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
        }

        string m_RoleText = string.Empty;

        /// <summary>
        /// Binds this row to a hierarchy node.
        /// </summary>
        public void Bind(UploadHierarchyNode node, string roleText = null, bool matchProjectStructure = false)
        {
            m_RoleText = roleText ?? string.Empty;
            m_MatchProjectStructure = matchProjectStructure;

            if (m_BoundNode == node)
            {
                // Still refresh dynamic state even if same node
                RefreshRoleChip();
                RefreshCheckbox();
                RefreshIgnoredState();
                RefreshProgressBar();
                RefreshReuploadCollectionWarningIcon();
                return;
            }

            // Unsubscribe from previous node's asset data events
            if (m_BoundNode?.AssetData != null)
            {
                m_BoundNode.AssetData.AssetDataChanged -= OnAssetDataChanged;
                m_AssetOperationManager.OperationProgressChanged -= OnOperationProgressChanged;
                m_AssetOperationManager.OperationFinished -= OnOperationProgressChanged;
                m_AssetOperationManager.OperationCleared -= OnOperationCleared;
            }

            m_BoundNode = node;

            if (node == null)
            {
                UIElementsUtils.Hide(this);
                return;
            }

            UIElementsUtils.Show(this);

            // Subscribe to asset data changes
            if (node.AssetData != null)
            {
                node.AssetData.AssetDataChanged += OnAssetDataChanged;
                m_AssetOperationManager.OperationProgressChanged += OnOperationProgressChanged;
                m_AssetOperationManager.OperationFinished += OnOperationProgressChanged;
                m_AssetOperationManager.OperationCleared += OnOperationCleared;
            }

            // Initialize context menu for asset nodes
            InitContextMenu(node);

            Refresh();
        }

        /// <summary>
        /// Unbinds this row from its current node.
        /// </summary>
        public void Unbind()
        {
            if (m_BoundNode?.AssetData != null)
            {
                m_BoundNode.AssetData.AssetDataChanged -= OnAssetDataChanged;
                m_AssetOperationManager.OperationProgressChanged -= OnOperationProgressChanged;
                m_AssetOperationManager.OperationFinished -= OnOperationProgressChanged;
                m_AssetOperationManager.OperationCleared -= OnOperationCleared;
            }

            // Clean up context menu
            if (m_ContextualMenuManipulator != null)
            {
                this.RemoveManipulator(m_ContextualMenuManipulator);
                m_ContextualMenuManipulator = null;
            }
            m_ContextMenu = null;

            m_BoundNode = null;
            UIElementsUtils.Hide(this);
        }

        void InitContextMenu(UploadHierarchyNode node)
        {
            // Only initialize context menu for asset nodes
            if (node?.AssetData == null)
            {
                // Remove existing context menu if node is not an asset
                if (m_ContextualMenuManipulator != null)
                {
                    this.RemoveManipulator(m_ContextualMenuManipulator);
                    m_ContextualMenuManipulator = null;
                    m_ContextMenu = null;
                }
                return;
            }

            var contextMenuBuilder = ServicesContainer.instance.Resolve<IContextMenuBuilder>();
            if (contextMenuBuilder == null)
                return;

            // Check if we need to create a new context menu
            if (m_ContextMenu == null ||
                !contextMenuBuilder.IsContextMenuMatchingAssetDataType(node.AssetData.GetType(), m_ContextMenu.GetType()))
            {
                // Remove old manipulator
                if (m_ContextualMenuManipulator != null)
                {
                    this.RemoveManipulator(m_ContextualMenuManipulator);
                }

                // Build new context menu
                m_ContextMenu = (AssetContextMenu)contextMenuBuilder.BuildContextMenu(node.AssetData.GetType());
                if (m_ContextMenu != null)
                {
                    m_ContextualMenuManipulator = new ContextualMenuManipulator(m_ContextMenu.SetupContextMenuEntries);
                    this.AddManipulator(m_ContextualMenuManipulator);
                }
            }

            // Update target asset data
            if (m_ContextMenu != null)
            {
                m_ContextMenu.TargetAssetData = node.AssetData;
            }
        }

        /// <summary>
        /// Sets whether the toggle is enabled.
        /// </summary>
        public void SetToggleEnabled(bool enabled)
        {
            m_Checkbox.SetEnabled(enabled);
        }

        /// <summary>
        /// Sets whether this row is selected.
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (m_IsSelected == selected)
                return;

            m_IsSelected = selected;
            EnableInClassList(UssStyles.RowSelected, selected);
        }

        void Refresh()
        {
            if (m_BoundNode == null)
                return;

            var node = m_BoundNode;

            // SuperRoot nodes should never be displayed - they are invisible containers
            // This is a safety check; the view should filter them out during flattening
            if (node.NodeType == HierarchyNodeType.SuperRoot)
            {
                UIElementsUtils.Hide(this);
                EnableInClassList(UssStyles.RowSuperRoot, true);
                return;
            }

            var isFolder = node.IsFolder;
            var isAsset = node.NodeType == HierarchyNodeType.Asset;
            var isProjectRoot = node.NodeType == HierarchyNodeType.ProjectRoot;

            // Update class lists
            EnableInClassList(UssStyles.RowSuperRoot, false);
            EnableInClassList(UssStyles.RowFolder, isFolder);
            EnableInClassList(UssStyles.RowAsset, isAsset);
            EnableInClassList(UssStyles.RowProjectRoot, isProjectRoot);

            // Indent
            m_IndentSpacer.style.width = node.Depth * k_IndentWidth;

            // Expand arrow
            RefreshExpandArrow();

            // Checkbox
            RefreshCheckbox();

            // Thumbnail
            RefreshThumbnail();

            // Status overlay
            RefreshStatusOverlay();

            // Name and extension
            m_NameLabel.text = node.Name;
            m_NameLabel.tooltip = isAsset && node.AssetData != null ? node.AssetData.AssetPath : node.Name;

            if (isAsset && !string.IsNullOrEmpty(node.Extension))
            {
                m_ExtensionLabel.text = node.Extension;
                UIElementsUtils.Show(m_ExtensionLabel);
            }
            else
            {
                UIElementsUtils.Hide(m_ExtensionLabel);
            }

            // Dependency icon (shown inline for linked assets)
            RefreshDependencyIcon();

            // Re-upload collection warning icon
            RefreshReuploadCollectionWarningIcon();

            // Role chip (for project root only)
            RefreshRoleChip();

            // Ignored state visual
            RefreshIgnoredState();

            // Progress bar
            RefreshProgressBar();
        }

        void RefreshRoleChip()
        {
            var isProjectRoot = m_BoundNode?.NodeType == HierarchyNodeType.ProjectRoot;
            var hasRole = !string.IsNullOrEmpty(m_RoleText);

            if (isProjectRoot && hasRole)
            {
                m_RoleChip.text = m_RoleText;
                UIElementsUtils.Show(m_RoleChip);
            }
            else
            {
                UIElementsUtils.Hide(m_RoleChip);
            }
        }

        void RefreshExpandArrow()
        {
            var node = m_BoundNode;

            if (!node.IsFolder || !node.HasChildren)
            {
                m_ExpandArrow.AddToClassList(UssStyles.ExpandArrowHidden);
                m_ExpandArrow.RemoveFromClassList(UssStyles.ExpandArrowExpanded);
                m_ExpandArrow.RemoveFromClassList(UssStyles.ExpandArrowCollapsed);
                return;
            }

            m_ExpandArrow.RemoveFromClassList(UssStyles.ExpandArrowHidden);
            m_ExpandArrow.EnableInClassList(UssStyles.ExpandArrowExpanded, node.IsExpanded);
            m_ExpandArrow.EnableInClassList(UssStyles.ExpandArrowCollapsed, !node.IsExpanded);
        }

        void RefreshCheckbox()
        {
            var node = m_BoundNode;
            var checkState = node.CheckState;

            // Handle tri-state
            m_Checkbox.showMixedValue = checkState == CheckState.Indeterminate;
            m_Checkbox.SetValueWithoutNotify(checkState == CheckState.Checked);

            m_Checkbox.EnableInClassList(UssStyles.CheckboxIndeterminate, checkState == CheckState.Indeterminate);

            // Determine if checkbox should be enabled
            if (node.NodeType == HierarchyNodeType.Asset)
            {
                var canBeIgnored = node.AssetData?.CanBeIgnored ?? false;
                m_Checkbox.SetEnabled(canBeIgnored);
            }
            else
            {
                // Folders can be toggled only if they have toggleable children
                var hasToggleableDescendants = node.HasToggleableDescendants;
                m_Checkbox.SetEnabled(hasToggleableDescendants);
            }
        }

        void RefreshThumbnail()
        {
            var node = m_BoundNode;
            var isProjectRoot = node.NodeType == HierarchyNodeType.ProjectRoot;
            var isFolderExpanded = node.IsFolder && !isProjectRoot && node.IsExpanded;
            var isFolderCollapsed = node.IsFolder && !isProjectRoot && !node.IsExpanded;

            m_Thumbnail.EnableInClassList(UssStyles.ThumbnailFolder, isFolderExpanded);
            m_Thumbnail.EnableInClassList(UssStyles.ThumbnailFolderCollapsed, isFolderCollapsed);
            m_Thumbnail.EnableInClassList(UssStyles.ThumbnailProjectRoot, isProjectRoot);

            if (node.IsFolder)
            {
                m_Thumbnail.style.backgroundImage = StyleKeyword.Null;
                return;
            }

            // For assets, try to get the thumbnail
            var assetData = node.AssetData;
            if (assetData?.Thumbnail != null)
            {
                m_Thumbnail.style.backgroundImage = assetData.Thumbnail;
            }
            else
            {
                // Use asset type icon as fallback
                var icon = AssetDataTypeHelper.GetIconForExtension(node.Extension);
                m_Thumbnail.style.backgroundImage = icon != null ? icon : StyleKeyword.Null;

                // Try to load thumbnail async if online
                if (assetData != null && m_UnityConnectProxy.AreCloudServicesReachable)
                {
                    _ = LoadThumbnailAsync(assetData);
                }
            }
        }

        async Task LoadThumbnailAsync(UploadAssetData assetData)
        {
            try
            {
                await assetData.GetThumbnailAsync();
            }
            catch
            {
                // Ignore thumbnail loading errors
            }
        }

        void RefreshStatusOverlay()
        {
            m_StatusOverlay.Clear();

            var node = m_BoundNode;
            if (node.NodeType != HierarchyNodeType.Asset || node.AssetData == null)
            {
                UIElementsUtils.Hide(m_StatusOverlay);
                return;
            }

            var statuses = AssetDataStatus.GetOverallStatus(node.AssetData.AssetDataAttributeCollection);
            var hasStatuses = false;

            foreach (var status in statuses)
            {
                // Skip null statuses and the Linked status (shown inline instead)
                if (status == null || status == AssetDataStatus.Linked)
                    continue;

                var statusElement = status.CreateVisualTree();
                statusElement.tooltip = L10n.Tr(status.Description);

                if (!string.IsNullOrEmpty(status.Details))
                {
                    statusElement.tooltip += $"\n\nDetails:\n{status.Details}";
                }

                m_StatusOverlay.Add(statusElement);
                hasStatuses = true;
            }

            UIElementsUtils.SetDisplay(m_StatusOverlay, hasStatuses);
        }

        void RefreshDependencyIcon()
        {
            var node = m_BoundNode;
            if (node.NodeType != HierarchyNodeType.Asset || node.AssetData == null)
            {
                UIElementsUtils.Hide(m_DependencyIcon);
                return;
            }

            var isLinked = node.AssetData.AssetDataAttributeCollection?
                .GetAttribute<LinkedDependencyAttribute>()?.IsLinked == true;

            if (isLinked)
            {
                m_DependencyIcon.tooltip = L10n.Tr(Constants.LinkedText);
                UIElementsUtils.Show(m_DependencyIcon);
            }
            else
            {
                UIElementsUtils.Hide(m_DependencyIcon);
            }
        }

        void RefreshReuploadCollectionWarningIcon()
        {
            var node = m_BoundNode;
            if (node?.NodeType != HierarchyNodeType.Asset || node.AssetData == null)
            {
                UIElementsUtils.Hide(m_ReuploadCollectionWarningIcon);
                return;
            }

            // Show warning when Match Project Structure is enabled and the asset will be added to a new collection
            // (i.e., it already exists with linked collections, but the new folder-based collection differs)
            var showWarning = m_MatchProjectStructure && node.AssetData.WillBeAddedToNewCollection;

            if (showWarning)
            {
                m_ReuploadCollectionWarningIcon.tooltip = L10n.Tr(Constants.ReuploadCollectionWarningText);
                UIElementsUtils.Show(m_ReuploadCollectionWarningIcon);
            }
            else
            {
                UIElementsUtils.Hide(m_ReuploadCollectionWarningIcon);
            }
        }

        void RefreshIgnoredState()
        {
            var node = m_BoundNode;
            var isIgnored = false;

            if (node.NodeType == HierarchyNodeType.Asset && node.AssetData != null)
            {
                isIgnored = node.AssetData.IsIgnored;
            }
            else if (node.IsFolder)
            {
                isIgnored = node.CheckState == CheckState.Unchecked;
            }

            EnableInClassList(UssStyles.RowIgnored, isIgnored);
        }

        void RefreshProgressBar()
        {
            var node = m_BoundNode;
            if (node?.NodeType != HierarchyNodeType.Asset || node.AssetData == null)
            {
                m_ProgressBar.Refresh(null);
                return;
            }

            var operation = m_AssetOperationManager.GetAssetOperation(node.AssetData.Identifier);
            m_ProgressBar.Refresh(operation);
        }

        void OnOperationProgressChanged(AssetDataOperation operation)
        {
            if (TrackedAssetIdentifier.IsFromSameAsset(operation.Identifier, m_BoundNode?.AssetData?.Identifier))
            {
                m_ProgressBar.Refresh(operation);
            }
        }

        void OnOperationCleared(TrackedAssetIdentifier identifier)
        {
            if (TrackedAssetIdentifier.IsFromSameAsset(identifier, m_BoundNode?.AssetData?.Identifier))
            {
                m_ProgressBar.Refresh(null);
            }
        }

        void OnExpandArrowClicked(ClickEvent evt)
        {
            if (m_BoundNode == null || !m_BoundNode.IsFolder || !m_BoundNode.HasChildren)
                return;

            evt.StopPropagation();
            ExpandClicked?.Invoke(m_BoundNode);
            RefreshExpandArrow();
            RefreshThumbnail();
        }

        void OnCheckboxValueChanged(ChangeEvent<bool> evt)
        {
            if (m_BoundNode == null)
                return;

            ToggleClicked?.Invoke(m_BoundNode, evt.newValue);
        }

        void OnRowClicked(ClickEvent evt)
        {
            if (m_BoundNode == null)
                return;

            // Don't trigger row selection if clicking on expand arrow or checkbox
            if (evt.target == m_ExpandArrow || evt.target == m_Checkbox || evt.target is Toggle)
                return;

            // Only asset nodes are selectable
            if (m_BoundNode.NodeType != HierarchyNodeType.Asset)
                return;

            RowClicked?.Invoke(m_BoundNode, evt.modifiers);
        }

        void OnMouseEnter(MouseEnterEvent evt)
        {
            AddToClassList(UssStyles.RowHovered);
        }

        void OnMouseLeave(MouseLeaveEvent evt)
        {
            RemoveFromClassList(UssStyles.RowHovered);
        }

        void OnAssetDataChanged(BaseAssetData assetData, AssetDataEventType eventType)
        {
            if (m_BoundNode?.AssetData != assetData)
                return;

            switch (eventType)
            {
                case AssetDataEventType.ThumbnailChanged:
                    RefreshThumbnail();
                    break;
                case AssetDataEventType.AssetDataAttributesChanged:
                    RefreshStatusOverlay();
                    RefreshDependencyIcon();
                    break;
                case AssetDataEventType.ToggleValueChanged:
                    RefreshCheckbox();
                    RefreshIgnoredState();
                    break;
                case AssetDataEventType.PropertiesChanged:
                    m_NameLabel.text = m_BoundNode.Name;
                    break;
            }
        }
    }
}
