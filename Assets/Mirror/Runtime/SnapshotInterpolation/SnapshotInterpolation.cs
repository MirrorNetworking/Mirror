// snapshot interpolation algorithms only,
// independent from Unity/NetworkTransform/MonoBehaviour/Mirror/etc.
// the goal is to remove all the magic from it.
// => a standalone snapshot interpolation algorithm
// => that can be simulated with unit tests easily
using System;
using System.Collections.Generic;

namespace Mirror
{
    public static class SnapshotInterpolation
    {
        // insert into snapshot buffer if newer than first entry
        // this should ALWAYS be used when inserting into a snapshot buffer!
        public static void InsertIfNewEnough<T>(T snapshot, SortedList<double, T> buffer)
            where T : Snapshot
        {
            // we need to drop any snapshot which is older ('<=')
            // the snapshots we are already working with.
            double timestamp = snapshot.remoteTimestamp;

            // if size == 1, then only add snapshots that are newer.
            // for example, a snapshot before the first one might have been
            // lagging.
            if (buffer.Count == 1 &&
                timestamp <= buffer.Values[0].remoteTimestamp)
                return;

            // for size >= 2, we are already interpolating between the first two
            // so only add snapshots that are newer than the second entry.
            // aka the 'ACB' problem:
            //   if we have a snapshot A at t=0 and C at t=2,
            //   we start interpolating between them.
            //   if suddenly B at t=1 comes in unexpectely,
            //   we should NOT suddenly steer towards B.
            if (buffer.Count >= 2 &&
                timestamp <= buffer.Values[1].remoteTimestamp)
                return;

            // otherwise sort it into the list
            buffer.Add(timestamp, snapshot);
        }

        // helper function to check if we have > 2 old enough snapshots.
        public static bool HasMoreThanTwoOldEnough<T>(SortedList<double, T> buffer, double threshold)
            where T : Snapshot =>
                buffer.Count >= 3 &&
                buffer.Values[2].localTimestamp <= threshold;

        // the core snapshot interpolation algorithm.
        // for a given remoteTime, interpolationTime and buffer,
        // we tick the snapshot simulation once.
        // => it's the same one on server and client
        // => should be called every Update() depending on authority
        //
        // time: LOCAL time since startup in seconds. like Unity's Time.time.
        // deltaTime: Time.deltaTime from Unity. parameter for easier tests.
        // interpolationTime: time in interpolation. moved along deltaTime.
        //                    between [0, delta] where delta is snapshot
        //                    B.timestamp - A.timestamp.
        //   IMPORTANT:
        //      => we use actual time instead of a relative
        //         t [0,1] because overshoot is easier to handle.
        //         if relative t overshoots but next snapshots are
        //         further apart than the current ones, it's not
        //         obvious how to calculate it.
        //      => for example, if t = 3 every time we skip we would have to
        //         make sure to adjust the subtracted value relative to the
        //         skipped delta. way too complex.
        //      => actual time can overshoot without problems.
        //         we know it's always by actual time.
        // bufferTime: time in seconds that we buffer snapshots.
        // buffer: our buffer of snapshots.
        //         Compute() assumes full integrity of the snapshots.
        //         for example, when interpolating between A=0 and C=2,
        //         make sure that you don't add B=1 between A and C if that
        //         snapshot arrived after we already started interpolating.
        //      => InsertIfNewEnough needs to protect against the 'ACB' problem
        // catchupThreshold: amount of buffer entries after which we start to
        //                   accelerate to catch up.
        //                   if 'bufferTime' is 'sendInterval * 3', then try
        //                   a value > 3 like 6.
        // catchupMultiplier: catchup by % per additional excess buffer entry
        //                    over the amount of 'catchupThreshold'.
        //
        // returns
        //   'true' if it spit out a snapshot to apply.
        //   'false' means computation moved along, but nothing to apply.
        public static bool Compute<T>(
            double time,
            double deltaTime,
            ref double interpolationTime,
            double bufferTime,
            SortedList<double, T> buffer,
            int catchupThreshold,
            float catchupMultiplier,
            out Snapshot computed)
                where T : Snapshot
        {
            // we buffer snapshots for 'bufferTime'
            // for example:
            //   * we buffer for 3 x sendInterval = 300ms
            //   * the idea is to wait long enough so we at least have a few
            //     snapshots to interpolate between
            //   * we process anything older 100ms immediately
            //
            // IMPORTANT: snapshot timestamps are _remote_ time
            // we need to interpolate and calculate buffer lifetimes based on it.
            // -> we don't know remote's current time
            // -> NetworkTime.time fluctuates too much, that's no good
            // -> we _could_ calculate an offset when the first snapshot arrives,
            //    but if there was high latency then we'll always calculate time
            //    with high latency
            // -> at any given time, we are interpolating from snapshot A to B
            // => seems like A.timestamp += deltaTime is a good way to do it

            computed = default;
            //Debug.Log($"{name} snapshotbuffer={buffer.Count}");

            // calculate catchup.
            // the goal is to buffer 'bufferTime' snapshots.
            // for whatever reason, we might see growing buffers.
            // in which case we should speed up to avoid ever growing delay.
            // -> first, calculate the excess
            int excess = buffer.Count - catchupThreshold;
            if (excess > 0)
            {
                // -> now calculate the total catch up
                double ketchup = excess * catchupMultiplier;

                // -> apply the catch up to time.
                // for example, assuming a catch up of 50%:
                // - deltaTime = 1s => 1.5s
                // - deltaTime = 0.1s => 0.15s
                // in other words, variations in deltaTime don't matter.
                // simply multiply. that's just how time works.
                // (50% catch up means 0.5, so we multiply by 1.5)
                deltaTime *= (1 + ketchup);
            }

            // interpolation always requires at least two snapshots,
            // and both need to be at least 'bufferTime' seconds old!
            // (because we always buffer for 'bufferTime' seconds first)
            // => first is always older than second
            // => only check if second is old enough
            // => by definition, first is older anyway
            double threshold = time - bufferTime;
            if (buffer.Count >= 2 &&
                // compare LOCAL time, not REMOTE time
                // (covered by Compute_Step3_WaitsUntilBufferTime test)
                buffer.Values[1].localTimestamp <= threshold)
            {
                Snapshot first = buffer.Values[0];
                Snapshot second = buffer.Values[1];

                // interpolationTime starts at 0 and we add deltaTime to move
                // along the interpolation.
                //
                // ONLY while we have snapshots to interpolate.
                // otherwise we might increase it to infinity which would lead
                // to skipping the next snapshots entirely.
                //
                // IMPORTANT: interpolationTime as actual time instead of
                // t [0,1] allows us to overshoot and subtract easily.
                // if t was [0,1], and we overshoot by 0.1, that's a
                // RELATIVE overshoot for the delta between B.time - A.time.
                // => if the next C.time - B.time is not the same delta,
                //    then the relative overshoot would speed up or slow
                //    down the interpolation! CAREFUL.
                //
                // IMPORTANT: we NEVER add deltaTime to 'time'.
                //            'time' is already NOW. that's how Unity works.
                interpolationTime += deltaTime;

                // delta between first & second is needed a lot
                double delta = second.remoteTimestamp - first.remoteTimestamp;

                // if interpolation time overshoots 'second' snapshot:
                // - subtract delta := second - first
                // - move to next one
                // - repeat as long as we still overshoot
                //
                // for example, if we have snapshots at:
                //   first:  t = 1
                //   second: t = 2
                //   third:  t = 3
                //   fourth: t = 4
                // and we currently interpolate between first & second.
                // after a slow update, interpolation time could be at t=3.5
                // so we should skip second & third and immediately start
                // interpolating between third & fourth.
                //
                // IMPORTANT: we don't set interpolationTime = next.time.
                //            this would cause jitter.
                //            we always want to subtract exactly delta.
                while (interpolationTime >= delta &&
                       // we can only move to next if we have more old enough
                       // snapshots.
                       //
                       // we already check if A & B are old enough before
                       // interpolating. we should also check if C is old enough
                       // before going there.
                       //
                       // if we wouldn't, then:
                       //   * B, C are now A, B.
                       //   * we would move there for one tick.
                       //   * next compute() does nothing again because A, B
                       //     aren't old enough.
                       //
                       // in other words: we NEVER move to a snapshot that's not
                       // older than bufferTime. neither when interpolating, nor
                       // when moving to the next one.
                       HasMoreThanTwoOldEnough(buffer, threshold))
                {
                    // subtract exactly delta from interpolation time
                    // instead of setting to '0', where we would lose the
                    // overshoot part and see jitter again.
                    //
                    // IMPORTANT: subtracting delta TIME works perfectly.
                    //            subtracting '1' from a ratio of t [0,1] would
                    //            leave the overshoot as relative between the
                    //            next delta. if next delta is different, then
                    //            overshoot would be bigger than planned and
                    //            speed up the interpolation.
                    interpolationTime -= delta;
                    //Debug.LogWarning($"{name} overshot and is now at: {interpolationTime}");

                    // remove first from buffer, move first to second & third
                    buffer.RemoveAt(0);
                    first = buffer.Values[0];
                    second = buffer.Values[1];

                    // 'first' and 'second' changed.
                    // so we need to recaculate 'delta' before the next
                    // check in the while loop.
                    // TODO this is too easy to miss. add a unit test!!!
                    delta = second.remoteTimestamp - first.remoteTimestamp;

                    // NOTE: it's worth consider spitting out all snapshots
                    // that we skipped, in case someone still wants to move
                    // along them to avoid physics collisions.
                    // * for NetworkTransform it's unnecessary as we always
                    //   set transform.position, which can go anywhere.
                    // * for CharacterController it's worth considering
                }

                // interpolationTime is actual time, NOT a 't' ratio [0,1].
                // we need 't' between [0,1] relative.
                // InverseLerp calculates just that.
                // InverseLerp CLAMPS between [0,1] and DOES NOT extrapolate!
                // => we already skipped ahead as many as possible above.
                // => we do NOT extrapolate for the reasons below.
                //
                // IMPORTANT:
                //   we should NOT extrapolate & predict while waiting for more
                //   snapshots as this would introduce a whole range of issues:
                //   * player might be extrapolated WAY out if we wait for long
                //   * player might be extrapolated behind walls
                //   * once we receive a new snapshot, we would interpolate
                //     not from the last valid position, but from the
                //     extrapolated position. this could be ANYWHERE. the
                //     player might get stuck in walls, etc.
                //   => we are NOT doing client side prediction & rollback here
                //   => we are simply interpolating with known, valid positions
                //
                // SEE TEST: Compute_Step5_OvershootWithoutEnoughSnapshots_NeverExtrapolates()
                double t = Mathd.InverseLerp(first.remoteTimestamp, second.remoteTimestamp, first.remoteTimestamp + interpolationTime);
                //Debug.Log($"InverseLerp({first.remoteTimestamp:F2}, {second.remoteTimestamp:F2}, {first.remoteTimestamp} + {interpolationTime:F2}) = {t:F2} snapshotbuffer={buffer.Count}");

                // interpolate snapshot, return true to indicate we computed one
                computed = first.Interpolate(second, t);

                // interpolationTime:
                // overshooting is ONLY allowed for smooth transitions when
                // immediately moving to the NEXT snapshot afterwards.
                //
                // if there is ANY break, for example:
                // * reached second snapshot and waiting for more
                // * reached second snapshot and next one isn't old enough yet
                //
                // then we SHOULD NOT overshoot because:
                // * increasing interpolationTime by deltaTime while waiting
                //   would make it grow HUGE to 100+.
                // * once we have more snapshots, we would skip most of them
                //   instantly instead of actually interpolating through them.
                if (!HasMoreThanTwoOldEnough(buffer, threshold))
                    interpolationTime = Math.Min(interpolationTime, second.remoteTimestamp);

                return true;
            }

            // no new snapshot was computed
            return false;
        }
    }
}
