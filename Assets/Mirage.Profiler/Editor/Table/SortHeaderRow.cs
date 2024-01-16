using System;
using UnityEngine.UIElements;

namespace Mirage.NetworkProfiler.ModuleGUI.UITable
{
    internal class SortHeaderRow : LabelRow
    {
        private readonly ITableSorter _sorter;
        private SortHeader _currentSort;

        public SortHeaderRow(Table table, ITableSorter sorter) : base(table, null)
        {
            _sorter = sorter ?? throw new ArgumentNullException(nameof(sorter));
        }

        protected override Label CreateLabel(ColumnInfo column)
        {
            var label = column.AllowSort
                ? new SortHeader(this)
                : new Label();

            SetLabelStyle(column, label);
            return label;
        }

        /// <summary>
        /// Update names of header based on sort
        /// </summary>
        /// <param name="sortHeader"></param>
        public void SetSortHeader(SortHeader sortHeader)
        {
            // not null or current
            if (_currentSort != null && _currentSort != sortHeader)
            {
                _currentSort.SortMode = SortMode.None;
                _currentSort.UpdateName();
            }

            _currentSort = sortHeader;
            _currentSort.UpdateName();
        }

        /// <summary>
        /// Updates names and applies sort to table
        /// </summary>
        /// <param name="sortHeader"></param>
        public void ApplySort(SortHeader sortHeader)
        {
            SetSortHeader(sortHeader);

            _sorter.Sort(Table, sortHeader);
        }
    }
}
