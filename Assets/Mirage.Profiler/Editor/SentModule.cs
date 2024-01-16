using Mirage.NetworkProfiler.ModuleGUI.Messages;
using Unity.Profiling.Editor;

namespace Mirage.NetworkProfiler.ModuleGUI
{
    [System.Serializable]
    [ProfilerModuleMetadata(ModuleNames.SENT)]
    public class SentModule : ProfilerModule, ICountRecorderProvider
    {
        private static readonly ProfilerCounterDescriptor[] counters = new ProfilerCounterDescriptor[]
        {
            new ProfilerCounterDescriptor(Names.SENT_COUNT, Counters.Category),
            new ProfilerCounterDescriptor(Names.SENT_BYTES, Counters.Category),
            new ProfilerCounterDescriptor(Names.SENT_PER_SECOND, Counters.Category),
        };

        public SentModule() : base(counters) { }

        public override ProfilerModuleViewController CreateDetailsViewController()
        {
            var names = new CounterNames(
                Names.SENT_COUNT,
                Names.SENT_BYTES,
                Names.SENT_PER_SECOND
            );

            return new MessageViewController(ProfilerWindow, names, SaveDataLoader.SentData);
        }

        CountRecorder ICountRecorderProvider.GetCountRecorder()
        {
            return NetworkProfilerRecorder._sentCounter;
        }
    }
}
