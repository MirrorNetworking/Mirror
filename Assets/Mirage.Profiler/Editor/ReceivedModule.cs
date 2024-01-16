using Mirage.NetworkProfiler.ModuleGUI.Messages;
using Unity.Profiling.Editor;

namespace Mirage.NetworkProfiler.ModuleGUI
{
    [System.Serializable]
    [ProfilerModuleMetadata(ModuleNames.RECEIVED)]
    public class ReceivedModule : ProfilerModule, ICountRecorderProvider
    {
        private static readonly ProfilerCounterDescriptor[] counters = new ProfilerCounterDescriptor[]
        {
            new ProfilerCounterDescriptor(Names.RECEIVED_COUNT, Counters.Category),
            new ProfilerCounterDescriptor(Names.RECEIVED_BYTES, Counters.Category),
            new ProfilerCounterDescriptor(Names.RECEIVED_PER_SECOND, Counters.Category),
        };

        public ReceivedModule() : base(counters) { }

        public override ProfilerModuleViewController CreateDetailsViewController()
        {
            var names = new CounterNames(
                Names.RECEIVED_COUNT,
                Names.RECEIVED_BYTES,
                Names.RECEIVED_PER_SECOND
            );

            return new MessageViewController(ProfilerWindow, names, SaveDataLoader.ReceiveData);
        }

        CountRecorder ICountRecorderProvider.GetCountRecorder()
        {
            return NetworkProfilerRecorder._receivedCounter;
        }
    }
}
