using UnityEngine.UIElements;

namespace Mirage.NetworkProfiler.ModuleGUI.UITable
{
    internal class SortHeader : Label
    {
        public const string ARROW_UP = "\u2191";
        public const string ARROW_DOWN = "\u2193";

        public SortMode SortMode;

        private readonly SortHeaderRow _row;

        private string _nameWithoutSort;
        public string NameWithoutSort
        {
            get
            {
                // this get should be called before any modifications are made
                // so lazy property should be safe here
                if (_nameWithoutSort == null)
                    _nameWithoutSort = text;

                return _nameWithoutSort;
            }
        }

        public ColumnInfo Info { get; internal set; }

        public SortHeader(SortHeaderRow row) : base()
        {
            _row = row;
            this.AddManipulator(new Clickable(OnClick));
        }

        private void OnClick(EventBase evt)
        {
            // just flip between Accending and Descending, no going back to none
            if (SortMode == SortMode.Accending)
                SortMode = SortMode.Descending;
            else
                SortMode = SortMode.Accending;

            _row.ApplySort(this);
        }

        public void UpdateName()
        {
            switch (SortMode)
            {
                case SortMode.None:
                    text = NameWithoutSort;
                    break;
                case SortMode.Accending:
                    text = ARROW_UP + NameWithoutSort;
                    break;
                case SortMode.Descending:
                    text = ARROW_DOWN + NameWithoutSort;
                    break;
            }
        }
    }
}
