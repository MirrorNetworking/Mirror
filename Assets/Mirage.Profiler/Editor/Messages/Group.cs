using System;
using System.Collections.Generic;
using Mirage.NetworkProfiler.ModuleGUI.UITable;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mirage.NetworkProfiler.ModuleGUI.Messages
{
    internal class Group
    {
        public readonly List<Row> Rows = new List<Row>();
        public readonly string Name;
        public readonly List<DrawnMessage> Messages = new List<DrawnMessage>();

        private readonly Table _table;
        private readonly Columns _columns;

        public Row Head;

        public int TotalBytes { get; private set; }
        public int TotalCount { get; private set; }
        public int Order { get; private set; }

        public bool Expanded { get; private set; }

        public Group(string name, Table table, Columns columns)
        {
            Name = name;
            _table = table;
            _columns = columns;
            // start at max, then take min each time message is added
            Order = int.MaxValue;
        }

        public void AddMessage(MessageInfo msg)
        {
            Messages.Add(new DrawnMessage { Info = msg });
            TotalBytes += msg.TotalBytes;
            TotalCount += msg.Count;
            Order = Math.Min(Order, msg.Order);
        }

        public void ToggleExpand()
        {
            Expand(!Expanded);
        }

        public void Expand(bool expanded)
        {
            Expanded = expanded;
            // create rows if needed
            LazyCreateRows();
            foreach (var row in Rows)
            {
                row.VisualElement.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // head can be null if ungrouped
            Head?.SetText(_columns.Expand, Expanded ? "-" : "+");
        }

        public void LazyCreateRows()
        {
            // not visible, do nothing till row is expanded
            if (!Expanded)
                return;
            // already created
            if (Rows.Count > 0)
                return;

            DrawMessages();
        }


        /// <param name="messages">Messages to add to table</param>
        /// <param name="createdRows">list to add rows to once created, Can be null</param>
        private void DrawMessages()
        {
            var previous = Head;
            var backgroundColor = GetBackgroundColor();

            foreach (var drawn in Messages)
            {
                var row = _table.AddRow(previous);
                Rows.Add(row);

                // set previous to be new row, so that message are added in order after previous
                previous = row;

                drawn.Row = row;
                var info = drawn.Info;
                DrawMessage(row, info);

                // set color of labels not whole row, otherwise color will be outside of table as well
                foreach (var ele in row.GetChildren())
                    ele.style.backgroundColor = backgroundColor;
            }
        }

        private void DrawMessage(Row row, MessageInfo info)
        {
            foreach (var column in _columns)
            {
                row.SetText(column, column.TextGetter.Invoke(info));
                if (column.HasToolTip)
                {
                    var label = row.GetLabel(column);
                    label.tooltip = column.ToolTipGetter.Invoke(info);
                }
            }
        }

        private static Color GetBackgroundColor()
        {
            // pick color that is lighter/darker than default editor background
            // todo check if there is a way to get the real color, or do we have to use `isProSkin`?
            return EditorGUIUtility.isProSkin
                ? (Color)new Color32(56, 56, 56, 255) / 0.8f
                : (Color)new Color32(194, 194, 194, 255) * .8f;
        }
    }
}
