// snapshot interpolation V2 by mischa
//
// Unity independent to be engine agnostic & easy to test.
// boxing: in C#, uses <T> does not box! passing the interface would box!
//
// credits:
//   glenn fiedler: https://gafferongames.com/post/snapshot_interpolation/
//   fholm: netcode streams
//   fakebyte: standard deviation for dynamic adjustment
//   ninjakicka: math & debugging
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Mirror
{
    public static class SortedListExtensions
    {
        // removes the first 'amount' elements from the sorted list
        public static void RemoveRange<T, U>(this SortedList<T, U> list, int amount)
        {
            // remove the first element 'amount' times.
            // handles -1 and > count safely.
            for (int i = 0; i < amount && i < list.Count; ++i)
                list.RemoveAt(0);
        }
    }

    public static class SnapshotInterpolation
    {
        // calculate dynamic buffer time adjustment
        public static double DynamicAdjustment(
            double sendInterval,
            double jitterStandardDeviation,
            double dynamicAdjustmentTolerance)
        {
            // jitter is equal to delivery time standard variation.
            // delivery time is made up of 'sendInterval+jitter'.
            //   .Average would be dampened by the constant sendInterval
            //   .StandardDeviation is the changes in 'jitter' that we want
            // so add it to send interval again.
            double intervalWithJitter = sendInterval + jitterStandardDeviation;

            // how many multiples of sendInterval is that?
            // we want to convert to bufferTimeMultiplier later.
            double multiples = intervalWithJitter / sendInterval;

            // add the tolerance
            double safezone = multiples + dynamicAdjustmentTolerance;
            // UnityEngine.Debug.Log($"sendInterval={sendInterval:F3} jitter std={jitterStandardDeviation:F3} => that is ~{multiples:F1} x sendInterval + {dynamicAdjustmentTolerance} => dynamic bufferTimeMultiplier={safezone}");
            return safezone;
        }

        // helper function to insert a snapshot if it doesn't exist yet.
        // extra function so we can use it for both cases:
        //   NetworkClient global timeline insertions & adjustments via Insert<T>.
        //   NetworkBehaviour local insertion without any time adjustments.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsertIfNotExists<T>(
            SortedList<double, T> buffer, // snapshot buffer
            T snapshot)                   // the newly received snapshot
            where T : Snapshot
        {
            // SortedList does not allow duplicates.
            // we don't need to check ContainsKey (which is expensive).
            // simply add and compare count before/after for the return value.

            //if (buffer.ContainsKey(snapshot.remoteTime)) return false; // too expensive
            // buffer.Add(snapshot.remoteTime, snapshot);                // throws if key exists

            int before = buffer.Count;
            buffer[snapshot.remoteTime] = snapshot; // overwrites if key exists
            return buffer.Count > before;
        }

        // call this for every received snapshot.
        // adds / inserts it to the list & initializes local time if needed.
        public static void InsertAndAdjust<T>(
            SortedList<double, T> buffer,                 // snapshot buffer
            T snapshot,                                   // the newly received snapshot
            double localTimeline,                         // local interpolation time based on server time
            double bufferTime,                            // offset for buffering
            ref ExponentialMovingAverage driftEma,        // for catchup / slowdown
            ref ExponentialMovingAverage deliveryTimeEma) // for dynamic buffer time adjustment
            where T : Snapshot
        {
            // first snapshot?
            // initialize local timeline.
            // we want it to be behind by 'offset'.
            //
            // note that the first snapshot may be a lagging packet.
            // so we would always be behind by that lag.
            // this requires catchup later.
            if (buffer.Count == 0)
                localTimeline = snapshot.remoteTime - bufferTime;

            // insert into the buffer.
            //
            // note that we might insert it between our current interpolation
            // which is fine, it adds another data point for accuracy.
            //
            // note that insert may be called twice for the same key.
            // by default, this would throw.
            // need to handle it silently.
            if (InsertIfNotExists(buffer, snapshot))
            {
                // dynamic buffer adjustment needs delivery interval jitter
                if (buffer.Count >= 2)
                {
                    // note that this is not entirely accurate for scrambled inserts.
                    //
                    // we always use the last two, not what we just inserted
                    // even if we were to use the diff for what we just inserted,
                    // a scrambled insert would still not be 100% accurate:
                    // => assume a buffer of AC, with delivery time C-A
                    // => we then insert B, with delivery time B-A
                    // => but then technically the first C-A wasn't correct,
                    //    as it would have to be C-B
                    //
                    // in practice, scramble is rare and won't make much difference
                    double previousLocalTime = buffer.Values[buffer.Count - 2].localTime;
                    double lastestLocalTime = buffer.Values[buffer.Count - 1].localTime;

                    // this is the delivery time since last snapshot
                    double localDeliveryTime = lastestLocalTime - previousLocalTime;

                    // feed the local delivery time to the EMA.
                    // this is what the original stream did too.
                    // our final dynamic buffer adjustment is different though.
                    // we use standard deviation instead of average.
                    deliveryTimeEma.Add(localDeliveryTime);
                }

                // adjust timescale to catch up / slow down after each insertion
                // because that is when we add new values to our EMA.

                // we want localTimeline to be about 'bufferTime' behind.
                // for that, we need the delivery time EMA.
                // snapshots may arrive out of order, we can not use last-timeline.
                // we need to use the inserted snapshot's time - timeline.
                double latestRemoteTime = snapshot.remoteTime;

                // calculate timediff after localTimeline override changes
                double timeDiff = latestRemoteTime - localTimeline;

                // next, calculate average of a few seconds worth of timediffs.
                // this gives smoother results.
                //
                // to calculate the average, we could simply loop through the
                // last 'n' seconds worth of timediffs, but:
                // - our buffer may only store a few snapshots (bufferTime)
                // - looping through seconds worth of snapshots every time is
                //   expensive
                //
                // to solve this, we use an exponential moving average.
                // https://en.wikipedia.org/wiki/Moving_average#Exponential_moving_average
                // which is basically fancy math to do the same but faster.
                // additionally, it allows us to look at more timeDiff values
                // than we sould have access to in our buffer :)
                driftEma.Add(timeDiff);

                // timescale depends on driftEma.
                // driftEma only changes when inserting.
                // therefore timescale only needs to be calculated when inserting.
                // saves CPU cycles in Update.

                // debug logging
                // UnityEngine.Debug.Log($"sendInterval={sendInterval:F3} bufferTime={bufferTime:F3} drift={drift:F3} driftEma={driftEma.Value:F3} timescale={localTimescale:F3} deliveryIntervalEma={deliveryTimeEma.Value:F3}");
            }
        }

        // sample snapshot buffer to find the pair around the given time.
        // returns indices so we can use it with RemoveRange to clear old snaps.
        // make sure to use use buffer.Values[from/to], not buffer[from/to].
        // make sure to only call this is we have > 0 snapshots.
        public static void Sample<T>(
            SortedList<double, T> buffer, // snapshot buffer
            double localTimeline,         // local interpolation time based on server time
            out int from,                 // the snapshot <= time
            out int to,                   // the snapshot >= time
            out double t)                 // interpolation factor
            where T : Snapshot
        {
            from = -1;
            to = -1;
            t = 0;

            // sample from [0,count-1] so we always have two at 'i' and 'i+1'.
            for (int i = 0; i < buffer.Count - 1; ++i)
            {
                // is local time between these two?
                T first = buffer.Values[i];
                T second = buffer.Values[i + 1];
                if (localTimeline >= first.remoteTime &&
                    localTimeline <= second.remoteTime)
                {
                    // use these two snapshots
                    from = i;
                    to = i + 1;
                    t = Mathd.InverseLerp(first.remoteTime, second.remoteTime, localTimeline);
                    return;
                }
            }

            // didn't find two snapshots around local time.
            // so pick either the first or last, depending on which is closer.

            // oldest snapshot ahead of local time?
            if (buffer.Values[0].remoteTime > localTimeline)
            {
                from = to = 0;
                t = 0;
            }
            // otherwise initialize both to the last one
            else
            {
                from = to = buffer.Count - 1;
                t = 0;
            }
        }

        // progress local timeline every update.
        //
        // ONLY CALL IF SNAPSHOTS.COUNT > 0!
        //
        // decoupled from Step<T> for easier testing and so we can progress
        // time only once in NetworkClient, while stepping for each component.
        //
        // adjust timeline based on 5 ranges:
        //
        //   clamp | slowdown | normal | catchup | clamp
        //
        // => normal: move forward by delta time as usual.
        // => slowdown/catchup: accelerate/slow down delta time by 1%.
        // => clamp: limit timeline if it gets too far off.
        //
        // only catch up / slow down for a little bit occasionally.
        // a consistent multiplier would never be exactly 1.0.
        //
        // note that slowdown/catchup is not enough, we also need to clamp:
        // clamp timeline for cases where it gets too far behind.
        // for example, a client app may go into the background and get updated
        // with 1hz for a while.  by the time it's back it's at least 30 frames
        // behind, possibly more if the transport also queues up. In this
        // scenario, at 1% catch up it took around 20+ seconds to finally catch
        // up. For these kinds of scenarios it will be better to snap / clamp.
        //
        // to reproduce, try snapshot interpolation demo and press the button to
        // simulate the client timeline at multiple seconds behind. it'll take
        // a long time to catch up if the timeline is a long time behind.
        //
        // returns InterpolationMode for debugging
        public static SnapshotMode StepTime<T>(
            SortedList<double, T> buffer, // snapshot buffer
            ref double localTimeline,// local interpolation time based on server time
            double deltaTime, // engine delta time (unscaled)
            double bufferTime,
            double driftEma,
            double catchupSpeed,  // in percent %
            double slowdownSpeed) // in percent %
                where T: Snapshot
        {
            // get the latest snapshot. it's closest to remote time.
            T latest = buffer.Values[buffer.Count - 1];

            // we want local timeline to always be 'bufferTime' behind remote.
            double targetTime = latest.remoteTime - bufferTime;

            // first, calculate how far we are currently away from bufferTime.
            // use driftEma for averaged, smoother results.
            // we don't want to be too nervous for catchup/slowdown.
            double drift = driftEma - bufferTime;

            // way too far behind: clamp hard.
            if (drift > bufferTime)
            {
                localTimeline = targetTime - bufferTime;
                return SnapshotMode.ClampBehind;
            }

            // way too far ahead. clamp hard.
            if (drift < bufferTime)
            {
                localTimeline = targetTime + bufferTime;
                return SnapshotMode.ClampAhead;
            }

            // just a little behind: move by delta time and accelerate n%.
            if (drift > bufferTime / 2)
            {
                localTimeline += deltaTime * (1 + catchupSpeed);
                return SnapshotMode.Catchup;
            }

            // just a little ahead: move by delta time and slow down n%.
            if (drift < bufferTime / 2)
            {
                localTimeline += deltaTime * (1 - slowdownSpeed);
                return SnapshotMode.Slowdown;
            }

            // otherwise we are within normal range.
            // move linearly.
            localTimeline += deltaTime;
            return SnapshotMode.Normal;
        }

        // sample, clear old.
        // call this every update.
        //
        // ONLY CALL IF SNAPSHOTS.COUNT > 0!
        //
        // returns true if there is anything to apply (requires at least 1 snap)
        //   from/to/t are out parameters instead of an interpolated 'computed'.
        //   this allows us to store from/to/t globally (i.e. in NetworkClient)
        //   and have each component apply the interpolation manually.
        //   besides, passing "Func Interpolate" would allocate anyway.
        public static void StepInterpolation<T>(
            SortedList<double, T> buffer, // snapshot buffer
            double localTimeline,         // local interpolation time based on server time
            out T fromSnapshot,           // we interpolate 'from' this snapshot
            out T toSnapshot,             // 'to' this snapshot
            out double t)                 // at ratio 't' [0,1]
            where T : Snapshot
        {
            // check this in caller:
            // nothing to do if there are no snapshots at all yet
            // if (buffer.Count == 0) return false;

            // sample snapshot buffer at local interpolation time
            Sample(buffer, localTimeline, out int from, out int to, out t);

            // save from/to
            fromSnapshot = buffer.Values[from];
            toSnapshot = buffer.Values[to];

            // remove older snapshots that we definitely don't need anymore.
            // after(!) using the indices.
            //
            // if we have 3 snapshots and we are between 2nd and 3rd:
            //   from = 1, to = 2
            // then we need to remove the first one, which is exactly 'from'.
            // because 'from-1' = 0 would remove none.
            buffer.RemoveRange(from);
        }

        // update time, sample, clear old.
        // call this every update.
        //
        // ONLY CALL IF SNAPSHOTS.COUNT > 0!
        //
        // returns InterpolationMode for debugging.
        //   from/to/t are out parameters instead of an interpolated 'computed'.
        //   this allows us to store from/to/t globally (i.e. in NetworkClient)
        //   and have each component apply the interpolation manually.
        //   besides, passing "Func Interpolate" would allocate anyway.
        public static SnapshotMode Step<T>(
            SortedList<double, T> buffer, // snapshot buffer
            ref double localTimeline,     // local interpolation time based on server time
            double deltaTime,             // engine delta time (unscaled)
            double bufferTime,
            double driftEma,
            double catchupSpeed,
            double slowdownSpeed,
            out T fromSnapshot,           // we interpolate 'from' this snapshot
            out T toSnapshot,             // 'to' this snapshot
            out double t)                 // at ratio 't' [0,1]
            where T : Snapshot
        {
            SnapshotMode mode = StepTime(buffer, ref localTimeline, deltaTime, bufferTime, driftEma, catchupSpeed, slowdownSpeed);
            StepInterpolation(buffer, localTimeline, out fromSnapshot, out toSnapshot, out t);
            return mode;
        }
    }
}
