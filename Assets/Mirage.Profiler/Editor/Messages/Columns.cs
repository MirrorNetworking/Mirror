using System.Collections;
using System.Collections.Generic;
using Mirage.NetworkProfiler.ModuleGUI.UITable;

namespace Mirage.NetworkProfiler.ModuleGUI.Messages
{
    internal sealed class Columns : IEnumerable<ColumnInfo>
    {
        private const int EXPAND_WIDTH = 25;
        private const int FULL_NAME_WIDTH = 300;
        private const int NAME_WIDTH = 150;
        private const int OTHER_WIDTH = 100;

        private readonly ColumnInfo[] _columns;

        public readonly ColumnInfo Expand;
        public readonly ColumnInfo FullName;
        public readonly ColumnInfo TotalBytes;
        public readonly ColumnInfo Count;
        public readonly ColumnInfo BytesPerMessage;
        public readonly ColumnInfo NetId;
        public readonly ColumnInfo ObjectName;
        public readonly ColumnInfo RpcName;

        public Columns()
        {
            Expand = new ColumnInfo("+", EXPAND_WIDTH, x => "");

            FullName = new ColumnInfo("Message", FULL_NAME_WIDTH, x => x.Name);
            FullName.AddSort(m => m.Name, m => m.Name);

            TotalBytes = new ColumnInfo("Total Bytes", OTHER_WIDTH, x => x.TotalBytes.ToString());
            TotalBytes.AddSort(m => m.TotalBytes, m => m.TotalBytes);

            Count = new ColumnInfo("Count", OTHER_WIDTH, x => x.Count.ToString());
            Count.AddSort(m => m.TotalCount, m => m.Count);

            BytesPerMessage = new ColumnInfo("Bytes", OTHER_WIDTH, x => x.Bytes.ToString());
            BytesPerMessage.AddSort(null, m => m.Bytes);

            NetId = new ColumnInfo("Net id", OTHER_WIDTH, x => x.NetId.HasValue ? x.NetId.ToString() : "");
            NetId.AddSort(null, m => m.NetId.GetValueOrDefault());

            ObjectName = new ColumnInfo("GameObject Name", NAME_WIDTH, x => x.ObjectName);
            ObjectName.AddSort(null, m => m.ObjectName);

            RpcName = new ColumnInfo("RPC Name (hover for full name)", FULL_NAME_WIDTH, x => RpcShortName(x.RpcName));
            RpcName.AddSort(null, m => m.RpcName);
            // full name in tooltip
            RpcName.AddToolTip(m => m.RpcName);

            _columns = new ColumnInfo[] {

                Expand,
                FullName,
                TotalBytes,
                Count,
                BytesPerMessage,
                NetId,
                ObjectName,
                RpcName,
            };
        }

        private string RpcShortName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return string.Empty;

            const char separator = '.';

            if (fullName.Contains(separator))
            {
                var split = fullName.Split(separator);
                var count = split.Length;
                if (count >= 2)
                {
                    return $"{split[count - 2]}.{split[count - 1]}";
                }
            }

            return fullName;
        }


        public IEnumerator<ColumnInfo> GetEnumerator() => ((IEnumerable<ColumnInfo>)_columns).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _columns.GetEnumerator();
    }
}
