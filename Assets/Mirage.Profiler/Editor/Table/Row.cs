using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Mirage.NetworkProfiler.ModuleGUI.UITable
{
    internal abstract class Row
    {
        public Table Table { get; }
        public VisualElement VisualElement { get; }

        public Row(Table table, Row previous = null)
        {
            Table = table;

            VisualElement = new VisualElement();
            VisualElement.style.flexDirection = FlexDirection.Row;

            var parent = table.ScrollView;
            if (previous != null)
            {
                var index = parent.IndexOf(previous.VisualElement);
                // insert after previous
                parent.Insert(index + 1, VisualElement);
            }
            else
            {
                // just add at end
                parent.Add(VisualElement);
            }
        }

        public abstract Label GetLabel(ColumnInfo column);
        public abstract IEnumerable<VisualElement> GetChildren();

        public void SetText(ColumnInfo column, object obj)
        {
            SetText(column, obj.ToString());
        }
        public void SetText(ColumnInfo column, string text)
        {
            var label = GetLabel(column);
            label.text = text;
        }
    }
}
