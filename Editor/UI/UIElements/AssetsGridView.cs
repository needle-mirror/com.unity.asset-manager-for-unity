using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AssetManager.Core.Editor;
using Unity.AssetManager.Upload.Editor;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.AssetManager.UI.Editor
{
    interface IGridItem
    {
        BaseAssetData AssetData { get; }
        void BindWithItem(BaseAssetData assetData);
    }

    class AssetsGridView : VisualElement
    {
        readonly GridView m_Gridview;
        readonly GridMessageView m_GridMessageView;
        readonly LoadingBar m_LoadingBar;

        readonly IUnityConnectProxy m_UnityConnect;
        readonly IPageManager m_PageManager;
        readonly IAssetDataManager m_AssetDataManager;
        readonly IAssetOperationManager m_AssetOperationManager;
        readonly IProjectOrganizationProvider m_ProjectOrganizationProvider;
        readonly IUploadManager m_UploadManager;
        readonly IAssetImporter m_AssetImporter;
        readonly IAssetsProvider m_AssetsProvider;

        bool m_IsClickedItemAlreadySelected;
        AssetIdentifier m_SelectedAssetIdentifier;

        public AssetsGridView(IProjectOrganizationProvider projectOrganizationProvider,
            IUnityConnectProxy unityConnect,
            IPageManager pageManager,
            IAssetDataManager assetDataManager,
            IAssetOperationManager assetOperationManager,
            ILinksProxy linksProxy,
            IUploadManager uploadManager,
            IAssetImporter assetImporter,
            IAssetsProvider assetsProvider)
        {
            m_UnityConnect = unityConnect;
            m_PageManager = pageManager;
            m_AssetDataManager = assetDataManager;
            m_AssetOperationManager = assetOperationManager;
            m_ProjectOrganizationProvider = projectOrganizationProvider;
            m_UploadManager = uploadManager;
            m_AssetImporter = assetImporter;
            m_AssetsProvider = assetsProvider;

            m_Gridview = new GridView(MakeGridViewItem, BindGridViewItem);
            Add(m_Gridview);

            m_GridMessageView = new GridMessageView(pageManager, projectOrganizationProvider, linksProxy);
            Add(m_GridMessageView);

            style.height = Length.Percent(100);

            m_LoadingBar = new LoadingBar();
            Add(m_LoadingBar);
            m_LoadingBar.Hide();

            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            ServicesContainer.instance.Resolve<IDragAndDropProjectBrowserProxy>().RegisterProjectBrowserHandler(OnProjectBrowserDrop);

            m_AssetsProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
            m_ProjectOrganizationProvider.OrganizationChanged += OnOrganizationChanged;

            m_Gridview.GridViewLastItemVisible += OnLastGridViewItemVisible;
            m_Gridview.BackgroundClicked += OnGridViewBackgroundClicked;
            m_PageManager.ActivePageChanged += OnActivePageChanged;
            m_PageManager.LoadingStatusChanged += OnLoadingStatusChanged;
            m_PageManager.MessageThrown += OnMessageThrown;
            m_PageManager.SelectedAssetChanged += OnSelectedAssetChanged;

            Refresh();
            TaskUtils.TrackException(WaitAFrameBeforeLoadingScrollDownReset());
        }

        async Task WaitAFrameBeforeLoadingScrollDownReset(){
            await Task.Delay(1);
            m_Gridview.LoadScrollOffset();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            ServicesContainer.instance.Resolve<IDragAndDropProjectBrowserProxy>().UnRegisterProjectBrowserHandler(OnProjectBrowserDrop);

            m_AssetsProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
            m_ProjectOrganizationProvider.OrganizationChanged -= OnOrganizationChanged;

            m_Gridview.GridViewLastItemVisible -= OnLastGridViewItemVisible;
            m_Gridview.BackgroundClicked -= OnGridViewBackgroundClicked;
            m_Gridview.SaveScrollOffset();

            m_PageManager.ActivePageChanged -= OnActivePageChanged;
            m_PageManager.LoadingStatusChanged -= OnLoadingStatusChanged;
            m_PageManager.MessageThrown -= OnMessageThrown;
            m_PageManager.SelectedAssetChanged -= OnSelectedAssetChanged;
        }

        void OnSelectedAssetChanged(IPage page, IEnumerable<AssetIdentifier> assets)
        {
            EditorApplication.delayCall -= DelayedScrollToSelectedAsset;

            if (assets.Any())
            {
                var asset = m_AssetDataManager.GetAssetData(assets.First());
                m_SelectedAssetIdentifier = asset.Identifier;
                EditorApplication.delayCall += DelayedScrollToSelectedAsset;
            }
        }

        void DelayedScrollToSelectedAsset()
        {
            try
            {
                m_Gridview.ScrollToRecycledRowOfItem(m_SelectedAssetIdentifier);
            }
            catch (Exception e)
            {
                Utilities.DevLogError($"Failed to scroll to asset: {e.Message}");
            }
            finally
            {
                EditorApplication.delayCall -= DelayedScrollToSelectedAsset;
            }
        }

        void OnGridViewBackgroundClicked()
        {
            m_PageManager.ActivePage.ClearSelection();
        }

        void OnAuthenticationStateChanged(AuthenticationState _)
        {
            Refresh();
        }

        void OnActivePageChanged(IPage page)
        {
            ClearGrid();
            Refresh();
        }

        VisualElement MakeGridViewItem()
        {
            var item = new GridItem(m_UnityConnect, m_AssetOperationManager, m_PageManager, m_AssetDataManager, m_UploadManager);

            item.PointerDownAction += GridItemOnPointerDown(item);
            item.PointerUpAction += GridItemOnPointerUp(item);
            item.DragStartedAction += GridItemOnDragStarted(item);

            return item;
        }

        DragAndDropVisualMode OnProjectBrowserDrop(int id, string path, bool perform)
        {
            var draggableObjects = DragAndDrop.objectReferences.OfType<DraggableObjectToImport>().ToList();
            if (draggableObjects.Count == 0)
                return DragAndDropVisualMode.None;

            if (perform)
            {
                var assetsData = draggableObjects.Select(x => x.AssetIdentifier).Select(x => m_AssetDataManager.GetAssetData(x)).ToList();
                if (assetsData.Count == 0)
                    return DragAndDropVisualMode.None;

                try
                {
                    m_AssetImporter.StartImportAsync(assetsData, ImportOperation.ImportType.UpdateToLatest, path);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            return DragAndDropVisualMode.Move;
        }

        Action GridItemOnDragStarted(GridItem item)
        {
            return () =>
            {
                // We need to be sure we can process this operation for the current event type
                var id = GUIUtility.GetControlID(FocusType.Passive);
                var evt = Event.current.GetTypeForControl(id);
                if (evt != EventType.MouseDrag && evt != EventType.MouseDown)
                    return;

                // We don't want to be able to drag items when we are on the UploadPage.
                if (m_PageManager.ActivePage is UploadPage)
                    return;

                Utilities.DevLog("Drag started on item " + item.AssetData.Name);
                // Clear existing data in DragAndDrop class.
                DragAndDrop.PrepareStartDrag();

                // Store reference to object and path to object in DragAndDrop static fields.
                var selectedAssets = new HashSet<AssetIdentifier> { item.AssetData.Identifier };
                foreach (var assetIdentifier in m_PageManager.ActivePage.SelectedAssets)
                {
                    selectedAssets.Add(assetIdentifier);
                }
                var objectReferences = selectedAssets.Select(identifier =>
                {
                    var draggableObj = ScriptableObject.CreateInstance<DraggableObjectToImport>();
                    draggableObj.AssetIdentifier = identifier;
                    return (Object)draggableObj;
                }).ToArray();

                DragAndDrop.objectReferences = objectReferences;

                // Start a drag.
                DragAndDrop.StartDrag($"Drag to Import {objectReferences.Length} Item" + (objectReferences.Length > 1 ? "s" : ""));
            };
        }

        Action<PointerDownEvent> GridItemOnPointerDown(GridItem item)
        {
            return e =>
            {
                if(e.target is Toggle || e.button != (int)MouseButton.LeftMouse)
                    return;

                m_IsClickedItemAlreadySelected =
                    m_PageManager.ActivePage.SelectedAssets.Contains(item.AssetData.Identifier);

                if (IsContinuousSelection(e.modifiers) && m_PageManager.ActivePage.SelectedAssets.Any())
                {
                    var lastSelectedItemIndex = m_PageManager.ActivePage.AssetList.ToList()
                        .FindIndex(x => x.Identifier.Equals(m_PageManager.ActivePage.LastSelectedAssetId));
                    var newSelectedItemIndex = m_PageManager.ActivePage.AssetList.ToList().IndexOf(item.AssetData);

                    var selectedAssets =
                        m_PageManager.ActivePage.AssetList.ToList()
                            .GetRange(Mathf.Min(lastSelectedItemIndex, newSelectedItemIndex),
                                Mathf.Abs(newSelectedItemIndex - lastSelectedItemIndex) + 1);

                    m_PageManager.ActivePage.SelectAssets(selectedAssets.Select(x => x.Identifier).ToList());
                }
                else if (!m_IsClickedItemAlreadySelected)
                {
                    m_PageManager.ActivePage.SelectAsset(item.AssetData.Identifier,
                        IsAdditiveSelection(e.modifiers));
                }
            };
        }

        Action<PointerUpEvent> GridItemOnPointerUp(GridItem item)
        {
            return e =>
            {
                if (m_IsClickedItemAlreadySelected)
                {
                    m_PageManager.ActivePage.SelectAsset(item.AssetData.Identifier,
                        IsAdditiveSelection(e.modifiers));
                }
            };
        }

        void BindGridViewItem(VisualElement element, int index)
        {
            var assetList = m_Gridview.ItemsSource as IList<BaseAssetData> ?? Array.Empty<BaseAssetData>();
            if (index < 0 || index >= assetList.Count)
                return;

            var assetId = assetList[index];

            var item = (IGridItem)element;
            item.BindWithItem(assetId);
        }

        void Refresh()
        {
            UIElementsUtils.Hide(m_Gridview);

            var page = m_PageManager.ActivePage;

            // The order matters since page is null if there is a Project Level error
            if (m_GridMessageView.Refresh() || page == null)
            {
                ClearGrid();
                return;
            }

            UIElementsUtils.Show(m_Gridview);

            m_Gridview.ItemsSource = page.AssetList.ToList();
            m_Gridview.Refresh(GridView.RefreshRowsType.ResizeGridWidth);
        }

        void ClearGrid()
        {
            Utilities.DevLog("Clearing grid...");
            m_Gridview.ItemsSource = Array.Empty<BaseAssetData>();
            m_Gridview.Refresh(GridView.RefreshRowsType.ClearGrid);
            m_Gridview.ResetScrollBarTop();
        }

        void OnLoadingStatusChanged(IPage page, bool isLoading)
        {
            if (!m_PageManager.IsActivePage(page))
                return;

            var hasAsset = page.AssetList?.Any() ?? false;

            if (isLoading)
            {
                m_LoadingBar.Show();
                m_LoadingBar.SetPosition(!hasAsset);
            }
            else
            {
                m_LoadingBar.Hide();
            }

            if (!page.IsLoading || !hasAsset)
            {
                Refresh();
            }
        }

        void OnLastGridViewItemVisible()
        {
            var page = m_PageManager.ActivePage;
            page.LoadMore();
        }

        void OnMessageThrown(IPage page, MessageData _)
        {
            if (!m_PageManager.IsActivePage(page))
                return;

            Refresh();
        }

        void OnOrganizationChanged(OrganizationInfo organization)
        {
            Refresh();
        }

        static bool IsContinuousSelection(EventModifiers eventModifiers)
        {
#if UNITY_EDITOR_OSX
            return (eventModifiers & EventModifiers.Shift) != 0 && (eventModifiers & EventModifiers.Command) == 0;
#else
            return (eventModifiers & EventModifiers.Shift) != 0;
#endif
        }

        static bool IsAdditiveSelection(EventModifiers eventModifiers)
        {
#if UNITY_EDITOR_OSX
            return (eventModifiers & EventModifiers.Command) != 0 && (eventModifiers & EventModifiers.Shift) != 0;
#else
            return (eventModifiers & EventModifiers.Control) != 0 && (eventModifiers & EventModifiers.Shift) == 0;
#endif
        }
    }
}
