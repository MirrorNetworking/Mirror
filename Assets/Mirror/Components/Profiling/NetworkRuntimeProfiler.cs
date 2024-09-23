using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mirror.RemoteCalls;
using UnityEngine;

namespace Mirror
{
    public class NetworkRuntimeProfiler : MonoBehaviour
    {
        [Serializable]
        public class Sorter : IComparer<Stat>
        {
            public SortBy Order;

            public int Compare(Stat a, Stat b)
            {
                if (a == null)
                    return 1;

                if (b == null)
                    return -1;

                // Compare B to A for desc order
                switch (Order)
                {
                    case SortBy.RecentBytes:
                        return b.RecentBytes.CompareTo(a.RecentBytes);
                    case SortBy.RecentCount:
                        return b.RecentCount.CompareTo(a.RecentCount);
                    case SortBy.TotalBytes:
                        return b.TotalBytes.CompareTo(a.TotalBytes);
                    case SortBy.TotalCount:
                        return b.TotalCount.CompareTo(a.TotalCount);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public enum SortBy
        {
            RecentBytes,
            RecentCount,
            TotalBytes,
            TotalCount
        }

        public class Stat
        {
            public string Name;
            public long TotalCount;
            public long TotalBytes;

            public long RecentCount;
            public long RecentBytes;

            public void ResetRecent()
            {
                RecentCount = 0;
                RecentBytes = 0;
            }

            public void Add(int count, int bytes)
            {
                TotalBytes += bytes;
                TotalCount += count;

                RecentBytes += bytes;
                RecentCount += count;
            }
        }

        class MessageStats
        {
            public readonly Dictionary<Type, Stat> MessageByType = new Dictionary<Type, Stat>();
            public readonly Dictionary<ushort, Stat> RpcByHash = new Dictionary<ushort, Stat>();

            public void Record(NetworkDiagnostics.MessageInfo info)
            {
                Type type = info.message.GetType();
                if (!MessageByType.TryGetValue(type, out Stat stat))
                {
                    stat = new Stat
                    {
                        Name = type.ToString(),
                        TotalCount = 0,
                        TotalBytes = 0,
                        RecentCount = 0,
                        RecentBytes = 0
                    };

                    MessageByType[type] = stat;
                }

                stat.Add(info.count, info.bytes * info.count);

                if (info.message is CommandMessage cmd)
                    RecordRpc(cmd.functionHash, info);
                else if (info.message is RpcMessage rpc)
                    RecordRpc(rpc.functionHash, info);
            }

            void RecordRpc(ushort hash, NetworkDiagnostics.MessageInfo info)
            {
                if (!RpcByHash.TryGetValue(hash, out Stat stat))
                {
                    string name = "n/a";
                    RemoteCallDelegate rpcDelegate = RemoteProcedureCalls.GetDelegate(hash);
                    if (rpcDelegate != null)
                        name = $"{rpcDelegate.Method.DeclaringType}.{rpcDelegate.GetMethodName().Replace(RemoteProcedureCalls.InvokeRpcPrefix, "")}";

                    stat = new Stat
                    {
                        Name = name,
                        TotalCount = 0,
                        TotalBytes = 0,
                        RecentCount = 0,
                        RecentBytes = 0
                    };

                    RpcByHash[hash] = stat;
                }

                stat.Add(info.count, info.bytes * info.count);
            }

            public void ResetRecent()
            {
                foreach (Stat stat in MessageByType.Values)
                    stat.ResetRecent();

                foreach (Stat stat in RpcByHash.Values)
                    stat.ResetRecent();
            }
        }

        [Tooltip("How many seconds to accumulate 'recent' stats for, this is also the output interval")]
        public float RecentDuration = 5;
        public Sorter Sort = new Sorter();

        public enum OutputType
        {
            UnityLog,
            StdOut,
            File
        }

        public OutputType Output;
        [Tooltip("If Output is set to 'File', where to the path of that file")]
        public string OutputFilePath = "network-stats.log";

        readonly MessageStats inStats = new MessageStats();
        readonly MessageStats outStats = new MessageStats();
        readonly StringBuilder printBuilder = new StringBuilder();
        float elapsedSinceReset;

        void Start()
        {
            // Ordering, Awake happens before NetworkDiagnostics reset
            NetworkDiagnostics.InMessageEvent += HandleMessageIn;
            NetworkDiagnostics.OutMessageEvent += HandleMessageOut;
        }

        void OnDestroy()
        {
            NetworkDiagnostics.InMessageEvent -= HandleMessageIn;
            NetworkDiagnostics.OutMessageEvent -= HandleMessageOut;
        }

        void HandleMessageOut(NetworkDiagnostics.MessageInfo info) => outStats.Record(info);

        void HandleMessageIn(NetworkDiagnostics.MessageInfo info) => inStats.Record(info);

        void LateUpdate()
        {
            elapsedSinceReset += Time.deltaTime;
            if (elapsedSinceReset > RecentDuration)
            {
                elapsedSinceReset = 0;
                Print();
                inStats.ResetRecent();
                outStats.ResetRecent();
            }
        }

        void Print()
        {
            printBuilder.Clear();
            printBuilder.AppendLine($"Stats for {DateTime.Now} ({RecentDuration:N1}s interval)");
            int nameMaxLength = "OUT Message".Length;

            foreach (Stat stat in inStats.MessageByType.Values)
                if (stat.Name.Length > nameMaxLength)
                    nameMaxLength = stat.Name.Length;

            foreach (Stat stat in outStats.MessageByType.Values)
                if (stat.Name.Length > nameMaxLength)
                    nameMaxLength = stat.Name.Length;

            foreach (Stat stat in inStats.RpcByHash.Values)
                if (stat.Name.Length > nameMaxLength)
                    nameMaxLength = stat.Name.Length;

            foreach (Stat stat in outStats.RpcByHash.Values)
                if (stat.Name.Length > nameMaxLength)
                    nameMaxLength = stat.Name.Length;

            string recentBytes = "Recent Bytes";
            string recentCount = "Recent Count";
            string totalBytes = "Total Bytes";
            string totalCount = "Total Count";
            int maxBytesLength = FormatBytes(999999).Length;
            int maxCountLength = FormatCount(999999).Length;

            int recentBytesPad = Mathf.Max(recentBytes.Length, maxBytesLength);
            int recentCountPad = Mathf.Max(recentCount.Length, maxCountLength);
            int totalBytesPad = Mathf.Max(totalBytes.Length, maxBytesLength);
            int totalCountPad = Mathf.Max(totalCount.Length, maxCountLength);
            string header = $"| {"IN Message".PadLeft(nameMaxLength)} | {recentBytes.PadLeft(recentBytesPad)} | {recentCount.PadLeft(recentCountPad)} | {totalBytes.PadLeft(totalBytesPad)} | {totalCount.PadLeft(totalCountPad)} |";
            string sep = "".PadLeft(header.Length, '-');
            printBuilder.AppendLine(sep);
            printBuilder.AppendLine(header);
            printBuilder.AppendLine(sep);

            foreach (Stat stat in inStats.MessageByType.Values.OrderBy(stat => stat, Sort))
                printBuilder.AppendLine($"| {stat.Name.PadLeft(nameMaxLength)} | {FormatBytes(stat.RecentBytes).PadLeft(recentBytesPad)} | {FormatCount(stat.RecentCount).PadLeft(recentCountPad)} | {FormatBytes(stat.TotalBytes).PadLeft(totalBytesPad)} | {FormatCount(stat.TotalCount).PadLeft(totalCountPad)} |");

            header = $"| {"IN RPCs".PadLeft(nameMaxLength)} | {recentBytes.PadLeft(recentBytesPad)} | {recentCount.PadLeft(recentCountPad)} | {totalBytes.PadLeft(totalBytesPad)} | {totalCount.PadLeft(totalCountPad)} |";
            printBuilder.AppendLine(sep);
            printBuilder.AppendLine(header);
            printBuilder.AppendLine(sep);
            foreach (Stat stat in inStats.RpcByHash.Values.OrderBy(stat => stat, Sort))
                printBuilder.AppendLine($"| {stat.Name.PadLeft(nameMaxLength)} | {FormatBytes(stat.RecentBytes).PadLeft(recentBytesPad)} | {FormatCount(stat.RecentCount).PadLeft(recentCountPad)} | {FormatBytes(stat.TotalBytes).PadLeft(totalBytesPad)} | {FormatCount(stat.TotalCount).PadLeft(totalCountPad)} |");

            header = $"| {"OUT Message".PadLeft(nameMaxLength)} | {recentBytes.PadLeft(recentBytesPad)} | {recentCount.PadLeft(recentCountPad)} | {totalBytes.PadLeft(totalBytesPad)} | {totalCount.PadLeft(totalCountPad)} |";
            printBuilder.AppendLine(sep);
            printBuilder.AppendLine(header);
            printBuilder.AppendLine(sep);
            foreach (Stat stat in outStats.MessageByType.Values.OrderBy(stat => stat, Sort))
                printBuilder.AppendLine($"| {stat.Name.PadLeft(nameMaxLength)} | {FormatBytes(stat.RecentBytes).PadLeft(recentBytesPad)} | {FormatCount(stat.RecentCount).PadLeft(recentCountPad)} | {FormatBytes(stat.TotalBytes).PadLeft(totalBytesPad)} | {FormatCount(stat.TotalCount).PadLeft(totalCountPad)} |");

            header = $"| {"OUT RPCs".PadLeft(nameMaxLength)} | {recentBytes.PadLeft(recentBytesPad)} | {recentCount.PadLeft(recentCountPad)} | {totalBytes.PadLeft(totalBytesPad)} | {totalCount.PadLeft(totalCountPad)} |";
            printBuilder.AppendLine(sep);
            printBuilder.AppendLine(header);
            printBuilder.AppendLine(sep);

            foreach (Stat stat in outStats.RpcByHash.Values.OrderBy(stat => stat, Sort))
                printBuilder.AppendLine($"| {stat.Name.PadLeft(nameMaxLength)} | {FormatBytes(stat.RecentBytes).PadLeft(recentBytesPad)} | {FormatCount(stat.RecentCount).PadLeft(recentCountPad)} | {FormatBytes(stat.TotalBytes).PadLeft(totalBytesPad)} | {FormatCount(stat.TotalCount).PadLeft(totalCountPad)} |");

            printBuilder.AppendLine(sep);

            switch (Output)
            {
                case OutputType.UnityLog:
                    Debug.Log(printBuilder.ToString());
                    break;
                case OutputType.StdOut:
                    Console.Write(printBuilder);
                    break;
                case OutputType.File:
                    File.AppendAllText(OutputFilePath, printBuilder.ToString());
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        static string FormatBytes(long bytes)
        {
            const double KiB = 1024;
            const double MiB = KiB * 1024;
            const double GiB = MiB * 1024;
            const double TiB = GiB * 1024;

            if (bytes < KiB)
                return $"{bytes:N0} B";

            if (bytes < MiB)
                return $"{bytes / KiB:N2} KiB";

            if (bytes < GiB)
                return $"{bytes / MiB:N2} MiB";

            if (bytes < TiB)
                return $"{bytes / GiB:N2} GiB";

            return $"{bytes / TiB:N2} TiB";
        }

        string FormatCount(long count)
        {
            const double K = 1000;
            const double M = K * 1000;
            const double G = M * 1000;
            const double T = G * 1000;

            if (count < K)
                return $"{count:N0}";

            if (count < M)
                return $"{count / K:N2} K";

            if (count < G)
                return $"{count / M:N2} M";

            if (count < T)
                return $"{count / G:N2} G";

            return $"{count / T:N2} T";
        }
    }
}
