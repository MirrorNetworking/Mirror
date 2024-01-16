using Unity.Profiling;

namespace Mirage.NetworkProfiler
{
    internal static class Counters
    {
        public static readonly ProfilerCategory Category = ProfilerCategory.Network;
        private const ProfilerMarkerDataUnit COUNT = ProfilerMarkerDataUnit.Count;
        private const ProfilerMarkerDataUnit BYTES = ProfilerMarkerDataUnit.Bytes;

        public static readonly ProfilerCounter<int> PlayerCount = new ProfilerCounter<int>(Category, Names.PLAYER_COUNT, COUNT);
        public static readonly ProfilerCounter<int> CharCount = new ProfilerCounter<int>(Category, Names.CHARACTER_COUNT, COUNT);
        public static readonly ProfilerCounter<int> ObjectCount = new ProfilerCounter<int>(Category, Names.OBJECT_COUNT, COUNT);

        public static readonly ProfilerCounter<int> SentCount = new ProfilerCounter<int>(Category, Names.SENT_COUNT, COUNT);
        public static readonly ProfilerCounter<int> SentBytes = new ProfilerCounter<int>(Category, Names.SENT_BYTES, BYTES);
        public static readonly ProfilerCounter<int> SentPerSecond = new ProfilerCounter<int>(Category, Names.SENT_PER_SECOND, BYTES);

        public static readonly ProfilerCounter<int> ReceiveCount = new ProfilerCounter<int>(Category, Names.RECEIVED_COUNT, COUNT);
        public static readonly ProfilerCounter<int> ReceiveBytes = new ProfilerCounter<int>(Category, Names.RECEIVED_BYTES, BYTES);
        public static readonly ProfilerCounter<int> ReceivePerSecond = new ProfilerCounter<int>(Category, Names.RECEIVED_PER_SECOND, BYTES);
    }
}
