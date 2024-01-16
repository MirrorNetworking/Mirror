using System;
using System.Collections.Generic;
using Mirage.NetworkProfiler.ModuleGUI.UITable;
using UnityEngine.UIElements;

namespace Mirage.NetworkProfiler.ModuleGUI.Messages
{
    internal class MessageView
    {
        private readonly Columns _columns;
        private readonly Table _table;

        /// <summary>
        /// cache list used to gather messages
        /// </summary>
        private readonly List<MessageInfo> _messages = new List<MessageInfo>();
        private readonly Dictionary<string, Group> _grouped = new Dictionary<string, Group>();

        public event Action<Group, bool> OnGroupExpanded;

        public MessageView(Columns columns, TableSorter sorter, VisualElement parent)
        {
            _columns = columns;
            _table = new Table(columns, sorter);
            parent.Add(_table.VisualElement);
        }

        public void Draw(IEnumerable<Frame> frames, bool groupMessages)
        {
            CollectMessages(frames);
            GroupMessages(groupMessages);
            DrawGroups(groupMessages);

            var expandColumn = _columns.Expand;
            var defaultWidth = expandColumn.Width;
            var width = groupMessages ? defaultWidth : 0;
            _table.ChangeWidth(expandColumn, width, true);
        }

        private void CollectMessages(IEnumerable<Frame> frames)
        {
            _messages.Clear();
            foreach (var frame in frames)
            {
                _messages.AddRange(frame.Messages);
            }
        }

        private void GroupMessages(bool asGroups)
        {
            _grouped.Clear();

            foreach (var message in _messages)
            {
                string name;
                if (asGroups)
                    name = message.Name;
                else
                    name = "all_messages";

                if (!_grouped.TryGetValue(name, out var group))
                {
                    group = new Group(name, _table, _columns);
                    _grouped[name] = group;
                }

                group.AddMessage(message);
            }
        }

        private void DrawGroups(bool withHeader)
        {
            foreach (var group in _grouped.Values)
            {
                if (withHeader)
                {
                    DrawGroupHeader(group);
                }
                else
                {
                    group.Expand(true);
                }
            }
        }

        private void DrawGroupHeader(Group group)
        {
            // draw header
            var head = _table.AddRow();
            head.SetText(_columns.Expand, group.Expanded ? "-" : "+");
            head.SetText(_columns.FullName, group.Name);
            head.SetText(_columns.TotalBytes, group.TotalBytes);
            head.SetText(_columns.Count, group.TotalCount);
            group.Head = head;

            var expand = head.GetLabel(_columns.Expand);
            expand.AddManipulator(new Clickable((evt) =>
            {
                group.ToggleExpand();
                OnGroupExpanded?.Invoke(group, group.Expanded);
            }));

            // will lazy create message if expanded
            group.Expand(group.Expanded);
        }

        public void Sort(ColumnInfo sortHeader, SortMode sortMode)
        {
            var sorter = new GroupSorter(_grouped, sortHeader, sortMode);
            sorter.Sort();

            if (sortHeader != null)
            {
                // also set table names, 
                _table.SetSortHeader(sortHeader, sortMode);
            }
        }

        public void Clear()
        {
            _table.Clear();
        }

        public VisualElement AddEmptyRow()
        {
            var row = _table.AddEmptyRow();
            return row.VisualElement;
        }

        public void ExpandMany(List<string> expanded)
        {
            foreach (var group in _grouped.Values)
            {
                if (expanded.Contains(group.Name))
                {
                    group.Expand(true);
                }
            }
        }
    }
}
