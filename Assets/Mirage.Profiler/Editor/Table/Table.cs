using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mirage.NetworkProfiler.ModuleGUI.UITable
{
    internal class Table
    {
        public readonly VisualElement VisualElement;
        public readonly ScrollView ScrollView;

        public readonly SortHeaderRow Header;
        public readonly List<Row> Rows = new List<Row>();
        public readonly IReadOnlyList<ColumnInfo> HeaderInfo;

        public bool ContainsEmptyRows { get; private set; }

        public Table(IEnumerable<ColumnInfo> columns, ITableSorter sorter)
        {
            // create readonly list from given Enumerable
            HeaderInfo = new List<ColumnInfo>(columns);

            // create table root and scroll view
            // root can be Horizontal scroll
            // Horizontal will also move header, but Vertical keeps header at top
            VisualElement = new ScrollView(ScrollViewMode.Horizontal);
            // seperate root for contents, so that Horizontal from scroll isn't applied
            var root = new VisualElement();
            VisualElement.Add(root);
            // using VerticalAndHorizontal fixes header being squashed, not sure why, or if it'll cause future problems
            ScrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);

            // add header to table root
            // header will initialize labels, but we need to set text
            Header = new SortHeaderRow(this, sorter);

            // add header and scroll to root
            root.Add(Header.VisualElement);
            root.Add(ScrollView);

            // add headers
            foreach (var c in columns)
            {
                var ele = Header.GetLabel(c);
                ele.text = c.Header;

                if (c.AllowSort)
                {
                    var sortHeader = (SortHeader)ele;
                    sortHeader.Info = c;
                }

                // make header element thicker
                var eleStyle = ele.style;
                eleStyle.unityFontStyleAndWeight = FontStyle.Bold;
                eleStyle.borderBottomWidth = 3;
                eleStyle.borderRightWidth = 3;
            }
        }

        public Row AddRow(Row previous = null)
        {
            var row = new LabelRow(this, previous);
            Rows.Add(row);
            return row;
        }

        public Row AddEmptyRow(Row previous = null)
        {
            var row = new EmptyRow(this, previous);
            ContainsEmptyRows = true;
            Rows.Add(row);
            return row;
        }

        public void ChangeWidth(ColumnInfo column, int newWidth, bool setVisibility)
        {
            foreach (var row in Rows)
            {
                if (row is EmptyRow)
                    continue;

                var label = row.GetLabel(column);
                var style = label.style;
                style.width = newWidth;

                if (setVisibility)
                    style.display = newWidth > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        /// Removes all rows expect header
        /// </summary>
        public void Clear()
        {
            ScrollView.Clear();
            Rows.Clear();
            Rows.Add(Header);
            ContainsEmptyRows = false;
        }

        /// <summary>
        /// Updates names of sort header
        /// </summary>
        public void SetSortHeader(ColumnInfo info, SortMode mode)
        {
            var sortBy = (SortHeader)Header.GetLabel(info);
            sortBy.SortMode = mode;
            Header.SetSortHeader(sortBy);
        }
    }
}
