using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Mirage.NetworkProfiler.ModuleGUI.UITable
{
    internal class EmptyRow : Row
    {
        public EmptyRow(Table table, Row previous = null) : base(table, previous) { }

        public override Label GetLabel(ColumnInfo column)
        {
            throw new NotSupportedException("Empty row does not have any columns");
        }

        public override IEnumerable<VisualElement> GetChildren()
        {
            return Enumerable.Empty<VisualElement>();
        }
    }
}
