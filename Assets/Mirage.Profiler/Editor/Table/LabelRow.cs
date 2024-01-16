using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mirage.NetworkProfiler.ModuleGUI.UITable
{
    internal class LabelRow : Row
    {
        private readonly Dictionary<ColumnInfo, Label> _elements = new Dictionary<ColumnInfo, Label>();

        public LabelRow(Table table, Row previous = null) : base(table, previous)
        {
            foreach (var header in table.HeaderInfo)
            {
                var label = CreateLabel(header);
                VisualElement.Add(label);
                _elements[header] = label;
            }
        }

        protected virtual Label CreateLabel(ColumnInfo column)
        {
            var label = new Label();
            SetLabelStyle(column, label);
            return label;
        }

        protected static void SetLabelStyle(ColumnInfo column, Label label)
        {
            var style = label.style;
            style.width = column.Width;

            style.paddingLeft = 5;
            style.paddingRight = 5;
            style.paddingTop = 5;
            style.paddingBottom = 5;
            style.borderRightColor = Color.white * .4f;
            style.borderBottomColor = Color.white * .4f;
            style.borderBottomWidth = 1;
            style.borderRightWidth = 2;
        }

        public override Label GetLabel(ColumnInfo column)
        {
            return _elements[column];
        }

        public override IEnumerable<VisualElement> GetChildren()
        {
            return _elements.Values;
        }
    }
}
