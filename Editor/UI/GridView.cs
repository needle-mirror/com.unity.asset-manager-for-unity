using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AssetManager.Editor
{
    internal class GridView : BindableElement, ISerializationCallbackReceiver
    {
        // Item height/widths are used to calculate the # of rows/columns
        public const int DefaultItemHeight = 150;
        public const int DefaultItemWidth = 125;

        const int k_ExtraVisibleRows = 2;
        const float k_FooterHeight = 40;
        const float k_MinSidePadding = DefaultItemWidth / 2f;
        private const string k_NoDataText = "No results found";
        internal int MaxVisibleItems;
        internal virtual event Action onGridViewLastItemVisible = delegate { };

        readonly ScrollView m_ScrollView;
        float m_ScrollOffset;
        float m_LastHeight;
        DateTime m_lastTime;

        // we keep this list in order to minimize temporary gc allocs
        List<RecycledRow> m_ScrollInsertionList = new List<RecycledRow>();

        internal Action<VisualElement, int> bindItem;
        internal Func<VisualElement> makeItem;

        IList m_ItemsSource;
        Func<int, int> m_GetItemId;
        int m_FirstVisibleIndex;

        List<RecycledRow> m_RowPool = new List<RecycledRow>();
        public int VisibleRowCount { get; private set; }

        int m_ItemHeight = DefaultItemHeight;
        int m_ItemWidth = DefaultItemWidth;
        int m_ColumnCount;

        private Label m_NoDataLabel;
        private bool m_RequestInProgress;

        public new class UxmlFactory : UxmlFactory<GridView> { }

        public GridView()
        {
            m_ScrollView = new ScrollView();
            m_ScrollView.AddToClassList("grid-view-scrollbar");
            m_ScrollView.StretchToParentSize();
            m_ScrollView.verticalScroller.valueChanged += OnScroll;

            RegisterCallback<GeometryChangedEvent>(OnSizeChanged);
            m_lastTime = DateTime.Now;
            hierarchy.Add(m_ScrollView);

            m_ScrollView.contentContainer.focusable = true;
            m_ScrollView.contentContainer.usageHints &=
                ~UsageHints
                    .GroupTransform; // Scroll views with virtualized content shouldn't have the "view transform" optimization

            focusable = true;
        }

        /// <summary>
        /// Constructs a <see cref="GridView"/>, with most required properties provided.
        /// </summary>
        /// <param name="makeItem">The factory method to call to create a display item. The method should return a
        /// VisualElement that can be bound to a data item.</param>
        /// <param name="bindItem">The method to call to bind a data item to a display item. The method
        /// receives as parameters the display item to bind, and the index of the data item to bind it to.</param>
        internal GridView(Func<VisualElement> makeItem, Action<VisualElement, int> bindItem) : this()
        {
            AddToClassList(Constants.GridViewStyleClassName);

            MakeItem = makeItem;
            BindItem = bindItem;
        }

        /// <summary>
        /// Constructs a <see cref="GridView"/>, with all required properties provided.
        /// </summary>
        /// <param name="itemsSource">The list of items to use as a data source.</param>
        /// <param name="makeItem">The factory method to call to create a display item. The method should return a
        /// VisualElement that can be bound to a data item.</param>
        /// <param name="bindItem">The method to call to bind a data item to a display item. The method
        /// receives as parameters the display item to bind, and the index of the data item to bind it to.</param>
        internal GridView(IList itemsSource, Func<VisualElement> makeItem, Action<VisualElement, int> bindItem) : this()
        {
            AddToClassList(Constants.GridViewStyleClassName);

            m_ItemsSource = itemsSource;

            MakeItem = makeItem;
            BindItem = bindItem;
        }

        /// <summary>
        /// Callback for binding a data item to the visual element.
        /// </summary>
        /// <remarks>
        /// The method called by this callback receives the VisualElement to bind, and the index of the
        /// element to bind it to.
        /// </remarks>
        Action<VisualElement, int> BindItem
        {
            get => bindItem;
            set
            {
                bindItem = value;
                Refresh();
            }
        }

        /// <summary>
        /// Callback for unbinding a data item from the VisualElement.
        /// </summary>
        /// <remarks>
        /// The method called by this callback receives the VisualElement to unbind, and the index of the
        /// element to unbind it from.
        /// </remarks>
        Action<VisualElement, int> UnbindItem { get; set; }

        /// <summary>
        /// Callback for constructing the VisualElement that is the template for each recycled and re-bound element in the list.
        /// </summary>
        /// <remarks>
        /// This callback needs to call a function that constructs a blank <see cref="VisualElement"/> that is
        /// bound to an element from the list.
        ///
        /// The GridView automatically creates enough elements to fill the visible area, and adds more if the area
        /// is expanded. As the user scrolls, the GridView cycles elements in and out as they appear or disappear.
        ///
        ///  This property must be set for the grid view to function.
        /// </remarks>
        Func<VisualElement> MakeItem
        {
            get => makeItem;
            set
            {
                if (makeItem == value)
                    return;
                makeItem = value;
                Refresh();
            }
        }

        /// <summary>
        /// The data source for list items.
        /// </summary>
        /// <remarks>
        /// This list contains the items that the <see cref="GridView"/> displays.
        /// This property must be set for the grid view to function.
        /// </remarks>
        internal IList ItemsSource
        {
            get => m_ItemsSource;
            set
            {
                m_ItemsSource = value;
                Refresh();

                AddNoResultTextIsNeeded();
            }
        }

        private void AddNoResultTextIsNeeded()
        {
            if ((m_ItemsSource == null || m_ItemsSource.Count == 0) && !RequestInProgress)
            {
                if (m_NoDataLabel == null)
                    m_NoDataLabel = new Label(k_NoDataText)
                    {
                        style = {
                                        top = Length.Percent(50),
                                        left = Length.Percent(43),
                                        fontSize = 16,
                                        unityFontStyleAndWeight= FontStyle.Bold
                                    }
                    };
                Add(m_NoDataLabel);
            }
            else
            {
                m_NoDataLabel?.RemoveFromHierarchy();
            }
        }

        float ResolvedItemHeight
        {
            get
            {
                // todo waiting for UI Toolkit to make Panel.scaledPixelsPerPoint public
                var dpiScaling = 1f;
                return Mathf.Round(ItemHeight * dpiScaling) / dpiScaling;
            }
        }

        /// <summary>
        /// Number of columns in the gridview
        /// </summary>
        public int ColumnCount
        {
            get => m_ColumnCount;
            set
            {
                if (m_ColumnCount != value && value > 0)
                {
                    m_ScrollOffset = 0;
                    m_ColumnCount = value;
                    Refresh();
                }
            }
        }

        /// <summary>
        /// Height of the GridItems used for vertical padding in the ScrollView
        /// </summary>
        public int ItemHeight
        {
            get => m_ItemHeight;
            set
            {
                if (m_ItemHeight != value && value > 0)
                {
                    m_ItemHeight = value;
                    m_ScrollView.verticalPageSize = m_ItemHeight;
                    Refresh();
                }
            }
        }

        internal bool RequestInProgress
        {
            get => m_RequestInProgress;
            set
            {
                m_RequestInProgress = value;
                AddNoResultTextIsNeeded();
            }
        }

        void OnScroll(float offset)
        {
            if (!HasValidDataAndBindings())
                return;

            m_ScrollOffset = offset;
            var pixelAlignedItemHeight = ResolvedItemHeight;
            var firstVisibleIndex = Mathf.FloorToInt(offset / pixelAlignedItemHeight) * ColumnCount;

            m_ScrollView.contentContainer.style.paddingTop = Mathf.FloorToInt(firstVisibleIndex / (float)ColumnCount) * pixelAlignedItemHeight;
            if (m_ScrollView.verticalScroller.value == m_ScrollView.verticalScroller.highValue || !m_ScrollView.visible)
            {
                onGridViewLastItemVisible?.Invoke();
            }

            if (firstVisibleIndex != m_FirstVisibleIndex)
            {
                m_FirstVisibleIndex = firstVisibleIndex;

                if (!m_RowPool.Any()) return;

                // we try to avoid rebinding a few items
                if (m_FirstVisibleIndex < m_RowPool[0].FirstIndex) //we're scrolling up
                {
                    //How many do we have to swap back
                    var count = m_RowPool[0].FirstIndex - m_FirstVisibleIndex;

                    var inserting = m_ScrollInsertionList;

                    for (var i = 0; i < count && m_RowPool.Count > 0; ++i)
                    {
                        var last = m_RowPool.Last();
                        inserting.Add(last);
                        m_RowPool.RemoveAt(m_RowPool.Count - 1); //we remove from the end

                        last.SendToBack(); //We send the element to the top of the list (back in z-order)
                    }

                    inserting.Reverse();

                    m_ScrollInsertionList = m_RowPool;
                    m_RowPool = inserting;
                    m_RowPool.AddRange(m_ScrollInsertionList);
                    m_ScrollInsertionList.Clear();
                }
                else if (m_FirstVisibleIndex > m_RowPool[0].FirstIndex) //down
                {
                    var inserting = m_ScrollInsertionList;

                    var checkIndex = 0;
                    while (checkIndex < m_RowPool.Count && m_FirstVisibleIndex > m_RowPool[checkIndex].FirstIndex)
                    {
                        var first = m_RowPool[checkIndex];
                        inserting.Add(first);
                        first.BringToFront(); //We send the element to the bottom of the list (front in z-order)
                        checkIndex++;
                    }

                    m_RowPool.RemoveRange(0, checkIndex); //we remove them all at once
                    m_RowPool.AddRange(inserting); // add them back to the end
                    inserting.Clear();
                }

                //Let's rebind everything
                for (var rowIndex = 0; rowIndex < m_RowPool.Count; rowIndex++)
                {
                    for (var colIndex = 0; colIndex < ColumnCount; colIndex++)
                    {
                        var index = rowIndex * ColumnCount + colIndex + m_FirstVisibleIndex;

                        if (index < ItemsSource.Count)
                        {
                            var item = m_RowPool[rowIndex].ElementAt(colIndex);
                            if (m_RowPool[rowIndex].indices[colIndex] == RecycledRow.undefinedIndex)
                            {
                                var newItem = MakeItem != null ? MakeItem.Invoke() : CreateDummyItemElement();
                                m_RowPool[rowIndex].RemoveAt(colIndex);
                                m_RowPool[rowIndex].Insert(colIndex, newItem);
                                item = newItem;
                            }

                            Setup(item, index);
                        }
                        else
                        {
                            var remainingOldItems = ColumnCount - colIndex;

                            while (remainingOldItems > 0)
                            {
                                m_RowPool[rowIndex].RemoveAt(colIndex);
                                m_RowPool[rowIndex].Insert(colIndex, CreateDummyItemElement());
                                m_RowPool[rowIndex].ids.RemoveAt(colIndex);
                                m_RowPool[rowIndex].ids.Insert(colIndex, RecycledRow.undefinedIndex);
                                m_RowPool[rowIndex].indices.RemoveAt(colIndex);
                                m_RowPool[rowIndex].indices.Insert(colIndex, RecycledRow.undefinedIndex);
                                remainingOldItems--;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Clears the GridView, recreates all visible visual elements, and rebinds all items.
        /// </summary>
        /// <remarks>
        /// Call this method whenever the data source changes.
        /// </remarks>
        internal void Refresh()
        {
            foreach (var recycledRow in m_RowPool)
            {
                recycledRow.Clear();
            }

            m_RowPool.Clear();
            m_ScrollView.Clear();
            VisibleRowCount = 0;

            if (!HasValidDataAndBindings())
                return;

            m_LastHeight = m_ScrollView.layout.height;

            if (float.IsNaN(m_LastHeight))
                return;

            m_FirstVisibleIndex = Math.Min((int)(m_ScrollOffset / ResolvedItemHeight) * ColumnCount,
                m_ItemsSource.Count - 1);
            ResizeHeight(m_LastHeight);

            var notEnoughItemToScroll = VisibleRowCount > 0 && m_LastHeight >= VisibleRowCount * ResolvedItemHeight;
            if (!notEnoughItemToScroll) return;
            m_ScrollView.contentContainer.style.paddingTop = 0;
        }

        internal void ResetScrollBarTop()
        {
            m_ScrollView.scrollOffset = new Vector2(0, 0);
            m_ScrollOffset = 0;
            m_ScrollView.contentContainer.style.paddingTop = 0;
        }

        void ResizeHeight(float height)
        {
            if (!HasValidDataAndBindings())
                return;

            var pixelAlignedItemHeight = ResolvedItemHeight;
            var rowCountForSource = Mathf.CeilToInt(ItemsSource.Count / (float)ColumnCount);
            var contentHeight = rowCountForSource * pixelAlignedItemHeight + k_FooterHeight;
            m_ScrollView.contentContainer.style.height = contentHeight;

            var scrollableHeight = Mathf.Max(0, contentHeight - m_ScrollView.contentViewport.layout.height);
            m_ScrollView.verticalScroller.highValue = scrollableHeight;

            var rowCountForHeight = Mathf.FloorToInt(height / pixelAlignedItemHeight) + k_ExtraVisibleRows;
            var rowCount = Math.Min(rowCountForHeight, rowCountForSource);
            MaxVisibleItems = rowCountForHeight * ColumnCount;

            if (VisibleRowCount != rowCount)
            {
                if (VisibleRowCount > rowCount)
                {
                    if (m_RowPool.Count > 0)
                    {
                        // Shrink
                        var removeCount = VisibleRowCount - rowCount;
                        for (var i = 0; i < removeCount; i++)
                        {
                            var lastIndex = m_RowPool.Count - 1;
                            m_RowPool[lastIndex].Clear();
                            m_ScrollView.Remove(m_RowPool[lastIndex]);
                            m_RowPool.RemoveAt(lastIndex);
                        }
                    }
                }
                else
                {
                    // Grow
                    var addCount = rowCount - VisibleRowCount;
                    for (var i = 0; i < addCount; i++)
                    {
                        var recycledRow = new RecycledRow(ResolvedItemHeight);

                        for (var indexInRow = 0; indexInRow < ColumnCount; indexInRow++)
                        {
                            var index = m_RowPool.Count * ColumnCount + indexInRow + m_FirstVisibleIndex;
                            var item = MakeItem != null && index < ItemsSource.Count
                                ? MakeItem.Invoke()
                                : CreateDummyItemElement();

                            recycledRow.Add(item);

                            if (index < ItemsSource.Count)
                            {
                                Setup(item, index);
                            }
                            else
                            {
                                recycledRow.ids.Add(RecycledRow.undefinedIndex);
                                recycledRow.indices.Add(RecycledRow.undefinedIndex);
                            }
                        }

                        m_RowPool.Add(recycledRow);
                        recycledRow.style.height = pixelAlignedItemHeight;

                        m_ScrollView.Add(recycledRow);
                    }
                }

                VisibleRowCount = rowCount;
            }

            m_LastHeight = height;
        }

        void Setup(VisualElement item, int newIndex)
        {
            var newId = GetIdFromIndex(newIndex);

            if (!(item.parent is RecycledRow recycledRow))
                throw new Exception("The item to setup can't be orphan");

            var indexInRow = recycledRow.IndexOf(item);

            if (recycledRow.indices.Count <= indexInRow)
            {
                recycledRow.indices.Add(RecycledRow.undefinedIndex);
                recycledRow.ids.Add(RecycledRow.undefinedIndex);
            }

            if (recycledRow.indices[indexInRow] == newIndex)
                return;

            if (recycledRow.indices[indexInRow] != RecycledRow.undefinedIndex)
                UnbindItem?.Invoke(item, recycledRow.indices[indexInRow]);

            recycledRow.indices[indexInRow] = newIndex;
            recycledRow.ids[indexInRow] = newId;

            BindItem.Invoke(item, recycledRow.indices[indexInRow]);
        }

        void OnSizeChanged(GeometryChangedEvent evt)
        {
            ColumnCount = Mathf.FloorToInt((evt.newRect.width - k_MinSidePadding) / m_ItemWidth);

            if (!HasValidDataAndBindings())
                return;

            var diff = DateTime.Now - m_lastTime;
            m_lastTime = DateTime.Now;

            if (diff.TotalSeconds < 1)
            {
                return;
            }

            if (Mathf.Approximately(evt.newRect.height, evt.oldRect.height) &&
                Mathf.Approximately(evt.newRect.width, evt.oldRect.width))
                return;

            var rowCount = Mathf.FloorToInt((evt.newRect.height - k_MinSidePadding) / m_ItemHeight);
            MaxVisibleItems = rowCount * ColumnCount;
            if (MaxVisibleItems > ItemsSource.Count)
            {
                onGridViewLastItemVisible?.Invoke();
            }
            ResizeHeight(evt.newRect.height);
        }

        VisualElement CreateDummyItemElement()
        {
            var item = new VisualElement();
            SetupDummyItemElement(item);
            return item;
        }

        int GetIdFromIndex(int index)
        {
            if (m_GetItemId == null)
                return index;
            return m_GetItemId(index);
        }

        bool HasValidDataAndBindings() => ItemsSource != null && MakeItem != null && BindItem != null;
        void SetupDummyItemElement(VisualElement item) => item.AddToClassList(Constants.GridViewDummyItemUssClassName);
        public void OnBeforeSerialize() { /* Do Nothing */ }
        public void OnAfterDeserialize() => Refresh();

        public class RecycledRow : VisualElement
        {
            internal const int undefinedIndex = -1;

            internal readonly List<int> ids;

            internal readonly List<int> indices;

            internal RecycledRow(float height)
            {
                AddToClassList(Constants.GridViewRowStyleClassName);
                style.height = height;

                indices = new List<int>();
                ids = new List<int>();
            }

            internal int FirstIndex => indices.Count > 0 ? indices[0] : undefinedIndex;
        }
    }
}
