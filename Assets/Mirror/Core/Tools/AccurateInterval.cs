// accurate interval from Mirror II.
// for sync / send intervals where it matters.
// does not(!) do catch-up.
//
// first, let's understand the problem.
//   say we need an interval of 10 Hz, so every 100ms in Update we do:
//     if (Time.time >= lastTime + interval)
//     {
//         lastTime = Time.time;
//         ...
//     }
//
//   this seems fine, but actually Time.time will always be a few ms beyond
//   the interval. but since lastTime is reset to Time.time, the remainder
//   is always ignored away.
//   with fixed tickRate servers (say 30 Hz), the remainder is significant!
//
//   in practice if we have a 30 Hz tickRate server with a 30 Hz sendRate,
//   the above way to measure the interval would result in a 18-19 Hz sendRate!
//   => this is not just a little off. this is _way_ off, by almost half.
//   => displaying actual + target tick/send rate will show this very easily.
//
// we need an accurate way to measure intervals for where it matters.
// and it needs to be testable to guarantee results.
using System.Runtime.CompilerServices;

namespace Mirror
{
    public static class AccurateInterval
    {
        // static func instead of storing interval + lastTime struct.
        // + don't need to initialize struct ctor with interval in Awake
        // + allows for interval changes at runtime too
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Elapsed(double time, double interval, ref double lastTime)
        {
            if (time >= lastTime + interval)
            {
                // naive implementation:
                //lastTime = time;

                // accurate but doesn't handle heavy load situations:
                //lastTime += interval;

                // heavy load edge case:
                // * interval is 100ms
                // * server is under heavy load, Updates slow down to 1/s
                // * Elapsed(1.000) returns true.
                //   technically 10 intervals have elapsed.
                // * server recovers to normal, Updates are every 10ms again
                // * Elapsed(1.010) should return false again until 1.100.
                //
                // increasing lastTime by interval would require 10 more calls
                // to ever catch up again:
                //   lastTime += interval
                //
                // as result, the next 10 calls to Elapsed would return true.
                //   Elapsed(1.001) => true
                //   Elapsed(1.002) => true
                //   Elapsed(1.003) => true
                //   ...
                // even though technically the delta was not >= interval.
                //
                // this would keep the server under heavy load, and it may never
                // catch-up. this is not ideal for large virtual worlds.
                //
                // instead, we want to skip multiples of 'interval' and only
                // keep the remainder.
                //
                // see also: AccurateIntervalTests.Slowdown()

                // easy to understand:
                //double elapsed = time - lastTime;
                //double remainder = elapsed % interval;
                //lastTime = time - remainder;

                // easier: set to rounded multiples of interval (fholm).
                // long to match double time.
                long multiplier = (long)(time / interval);
                lastTime = multiplier * interval;
                return true;
            }
            return false;
        }
    }
}
