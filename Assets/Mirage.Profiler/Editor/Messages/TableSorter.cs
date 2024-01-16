using Mirage.NetworkProfiler.ModuleGUI.UITable;
using UnityEngine;

namespace Mirage.NetworkProfiler.ModuleGUI.Messages
{

    internal class TableSorter : ITableSorter
    {
        private readonly MessageViewController _controller;

        public TableSorter(MessageViewController controller)
        {
            _controller = controller;
        }

        public void Sort(Table table, SortHeader header)
        {
            if (table.ContainsEmptyRows)
            {
                Debug.LogWarning("Can't sort when there are empty rows");
                return;
            }

            _controller.Sort(header);
        }
    }
}
