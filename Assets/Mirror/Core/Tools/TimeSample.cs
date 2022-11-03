// TimeSample from Mirror II.
// simple profiling sample, averaged for display in statistics.
// usable in builds without unitiy profiler overhead etc.
//
// .average may safely be called from main thread while Begin/End is in another.
// i.e. worker threads, transport, etc.
using System.Diagnostics;
using System.Threading;

namespace Mirror
{
    public struct TimeSample
    {
        // UnityEngine.Time isn't thread safe. use stopwatch instead.
        readonly Stopwatch watch;

        // remember when Begin was called
        double beginTime;

        // keep accumulating times over the given interval.
        // (not readonly. we modify its contents.)
        ExponentialMovingAverage ema;

        // average in seconds.
        // code often runs in sub-millisecond time. float is more precise.
        //
        // set with Interlocked for thread safety.
        // can be read from main thread while sampling happens in other thread.
        public double average; // THREAD SAFE

        // average over N begin/end captures
        public TimeSample(int n)
        {
            watch     = new Stopwatch();
            watch.Start();
            ema       = new ExponentialMovingAverage(n);
            beginTime = 0;
            average   = 0;
        }

        // begin is called before the code to be sampled
        public void Begin()
        {
            // remember when Begin was called.
            // keep StopWatch running so we can average over the given interval.
            beginTime = watch.Elapsed.TotalSeconds;
            // Debug.Log($"Begin @ {beginTime:F4}");
        }

        // end is called after the code to be sampled
        public void End()
        {
            // add duration in seconds to accumulated durations
            double elapsed = watch.Elapsed.TotalSeconds - beginTime;
            ema.Add(elapsed);

            // expose new average thread safely
            Interlocked.Exchange(ref average, ema.Value);
        }
    }
}
