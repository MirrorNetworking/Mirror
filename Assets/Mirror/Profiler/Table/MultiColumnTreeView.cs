using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Assertions;

namespace Mirror.Profiler.Table
{
    internal class MultiColumnTreeView : TreeViewWithTreeModel<MyTreeElement>
    {
        const float KRowHeights = 20f;
        const float KToggleWidth = 18f;
        public bool showControls = true;

        // All columns
        enum MyColumns
        {
            Direction,
            Name,
            Object,
            Count,
            Bytes,
            TotalBytes,
            Channel,
        }

        public enum SortOption
        {
            Direction,
            Name,
            Object,
            Count,
            Bytes,
            TotalBytes,
            Channel,
        }

        // Sort options per column
        readonly SortOption[] m_SortOptions =
        {
            SortOption.Direction,
            SortOption.Name,
            SortOption.Object,
            SortOption.Count,
            SortOption.Bytes,
            SortOption.TotalBytes,
            SortOption.Channel
        };

        public static void TreeToList(TreeViewItem root, IList<TreeViewItem> result)
        {
            if (root == null)
                throw new NullReferenceException("root");
            if (result == null)
                throw new NullReferenceException("result");

            result.Clear();

            if (root.children == null)
                return;

            Stack<TreeViewItem> stack = new Stack<TreeViewItem>();
            for (int i = root.children.Count - 1; i >= 0; i--)
                stack.Push(root.children[i]);

            while (stack.Count > 0)
            {
                TreeViewItem current = stack.Pop();
                result.Add(current);

                if (current.hasChildren && current.children[0] != null)
                {
                    for (int i = current.children.Count - 1; i >= 0; i--)
                    {
                        stack.Push(current.children[i]);
                    }
                }
            }
        }

        public MultiColumnTreeView(TreeViewState state, MultiColumnHeader multicolumnHeader, TreeModel<MyTreeElement> model) : base(state, multicolumnHeader, model)
        {
            Assert.AreEqual(m_SortOptions.Length, Enum.GetValues(typeof(MyColumns)).Length, "Ensure number of sort options are in sync with number of MyColumns enum values");

            // Custom setup
            rowHeight = KRowHeights;
            columnIndexForTreeFoldouts = 1;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            customFoldoutYOffset = (KRowHeights - EditorGUIUtility.singleLineHeight) * 0.5f; // center foldout in the row since we also center content. See RowGUI
            extraSpaceBeforeIconAndLabel = KToggleWidth;
            multicolumnHeader.sortingChanged += OnSortingChanged;

            Reload();
        }


        // Note we We only build the visible rows, only the backend has the full tree information. 
        // The treeview only creates info for the row list.
        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            IList<TreeViewItem> rows = base.BuildRows(root);
            SortIfNeeded(root, rows);
            return rows;
        }

        void OnSortingChanged(MultiColumnHeader _)
        {
            SortIfNeeded(rootItem, GetRows());
        }

        void SortIfNeeded(TreeViewItem root, IList<TreeViewItem> rows)
        {
            if (rows.Count <= 1)
                return;

            if (multiColumnHeader.sortedColumnIndex == -1)
            {
                return; // No column to sort for (just use the order the data are in)
            }

            // Sort the roots of the existing tree items
            SortByMultipleColumns();
            TreeToList(root, rows);
            Repaint();
        }

        void SortByMultipleColumns()
        {
            int[] sortedColumns = multiColumnHeader.state.sortedColumns;

            if (sortedColumns.Length == 0)
                return;

            IEnumerable<TreeViewItem<MyTreeElement>> myTypes = rootItem.children.Cast<TreeViewItem<MyTreeElement>>();
            IOrderedEnumerable<TreeViewItem<MyTreeElement>> orderedQuery = InitialOrder(myTypes, sortedColumns);
            for (int i = 1; i < sortedColumns.Length; i++)
            {
                SortOption sortOption = m_SortOptions[sortedColumns[i]];
                bool ascending = multiColumnHeader.IsSortedAscending(sortedColumns[i]);

                switch (sortOption)
                {
                    case SortOption.Direction:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.message.Direction, ascending);
                        break;
                    case SortOption.Name:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.name, ascending);
                        break;
                    case SortOption.Object:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.message.Object, ascending);
                        break;
                    case SortOption.Count:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.message.Count, ascending);
                        break;
                    case SortOption.Bytes:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.message.Size, ascending);
                        break;
                    case SortOption.TotalBytes:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.message.Size * l.data.message.Count, ascending);
                        break;
                    case SortOption.Channel:
                        orderedQuery = orderedQuery.ThenBy(l => l.data.message.Channel, ascending);
                        break;
                }
            }

            rootItem.children = orderedQuery.Cast<TreeViewItem>().ToList();
        }

        IOrderedEnumerable<TreeViewItem<MyTreeElement>> InitialOrder(IEnumerable<TreeViewItem<MyTreeElement>> myTypes, int[] history)
        {
            SortOption sortOption = m_SortOptions[history[0]];
            bool ascending = multiColumnHeader.IsSortedAscending(history[0]);
            switch (sortOption)
            {
                case SortOption.Direction:
                    return myTypes.Order(l => l.data.message.Direction, ascending);
                case SortOption.Name:
                    return myTypes.Order(l => l.data.name, ascending);
                case SortOption.Object:
                    return myTypes.Order(l => l.data.message.Object, ascending);
                case SortOption.Count:
                    return myTypes.Order(l => l.data.message.Count, ascending);
                case SortOption.Bytes:
                    return myTypes.Order(l => l.data.message.Size, ascending);
                case SortOption.TotalBytes:
                    return myTypes.Order(l => l.data.message.Size * l.data.message.Count, ascending);
                case SortOption.Channel:
                    return myTypes.Order(l => l.data.message.Channel, ascending);
                default:
                    Assert.IsTrue(false, "Unhandled enum");
                    break;
            }

            // default
            return myTypes.Order(l => l.data.name, ascending);
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            TreeViewItem<MyTreeElement> item = (TreeViewItem<MyTreeElement>)args.item;

            for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
            {
                CellGUI(args.GetCellRect(i), item, (MyColumns)args.GetColumn(i), ref args);
            }
        }

        void CellGUI(Rect cellRect, TreeViewItem<MyTreeElement> item, MyColumns column, ref RowGUIArgs args)
        {
            // Center cell rect vertically (makes it easier to place controls, icons etc in the cells)
            CenterRectUsingSingleLineHeight(ref cellRect);

            string value = "";

            switch (column)
            {
                case MyColumns.Direction:
                    value = item.data.message.Direction == NetworkDirection.Incoming ? "In" : "Out";
                    break;

                case MyColumns.Name:
                    value = item.data.name;
                    break;

                case MyColumns.Object:
                    value = item.data.message.Object;
                    break;

                case MyColumns.Count:
                    value = item.data.message.Count.ToString();
                    break;

                case MyColumns.Bytes:
                    value = item.data.message.Size.ToString();
                    break;

                case MyColumns.TotalBytes:
                    value = (item.data.message.Size * item.data.message.Count).ToString();
                    break;

                case MyColumns.Channel:
                    value = item.data.message.Channel < 0 ? "" : item.data.message.Channel.ToString();
                    break;

            }

            DefaultGUI.Label(cellRect, value, args.selected, args.focused);
        }

        protected override void SingleClickedItem(int id)
        {
            base.SingleClickedItem(id);

            IList<TreeViewItem> rows = GetRows();

            TreeViewItem<MyTreeElement> item = (TreeViewItem<MyTreeElement>)rows[id - 1];

            GameObject gameobject = item.data.message.GameObject;
            if (gameobject != null)
            {
                Selection.activeGameObject = gameobject;
            }
        }
        // Misc
        //--------

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return true;
        }

        public static MultiColumnHeaderState CreateDefaultMultiColumnHeaderState()
        {
            MultiColumnHeaderState.Column[] columns = {
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("In/Out"),
                    contextMenuText = "In/Out",
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 50,
                    minWidth = 30,
                    maxWidth = 60,
                    autoResize = false,
                    allowToggleVisibility = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Name"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 110,
                    minWidth = 60,
                    autoResize = true,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Object"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 110,
                    minWidth = 60,
                    autoResize = true,
                    allowToggleVisibility = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Count", "How many observers received the message" ),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 70,
                    minWidth = 60,
                    autoResize = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Bytes", "How big was the message.  Not including transport headers"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 70,
                    minWidth = 60,
                    autoResize = false,
                    allowToggleVisibility = true
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Total Bytes", "Total amount of bytes sent (Count * Bytes) "),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 70,
                    minWidth = 60,
                    autoResize = false
                },
                new MultiColumnHeaderState.Column
                {
                    headerContent = new GUIContent("Channel", "Channel through which the message was sent"),
                    headerTextAlignment = TextAlignment.Left,
                    sortedAscending = true,
                    sortingArrowAlignment = TextAlignment.Right,
                    width = 70,
                    minWidth = 60,
                    autoResize = false
                }
            };

            Assert.AreEqual(columns.Length, Enum.GetValues(typeof(MyColumns)).Length, "Number of columns should match number of enum values: You probably forgot to update one of them.");

            MultiColumnHeaderState state = new MultiColumnHeaderState(columns);
            return state;
        }
    }

    static class MyExtensionMethods
    {
        public static IOrderedEnumerable<T> Order<T, TKey>(this IEnumerable<T> source, Func<T, TKey> selector, bool ascending)
        {
            if (ascending)
            {
                return source.OrderBy(selector);
            }
            return source.OrderByDescending(selector);
        }

        public static IOrderedEnumerable<T> ThenBy<T, TKey>(this IOrderedEnumerable<T> source, Func<T, TKey> selector, bool ascending)
        {
            if (ascending)
            {
                return source.ThenBy(selector);
            }
            return source.ThenByDescending(selector);
        }
    }
}
