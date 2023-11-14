// standalone, easy to test algorithms for prediction
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    // prediction may capture Rigidbody3D/2D/etc. state
    // have a common interface.
    public interface PredictedState
    {
        double timestamp { get; }

        // predicted states should have absolute and delta values, for example:
        //   Vector3 position;
        //   Vector3 positionDelta; // from last to here
        // when inserting a correction between this one and the one before,
        // we need to adjust the delta:
        //   positionDelta *= multiplier;
        void AdjustDeltas(float multiplier);
    }

    public static class Prediction
    {
        // get the two states closest to a given timestamp.
        // those can be used to interpolate the exact state at that time.
        public static bool Sample<T>(
            SortedList<double, T> history,
            double timestamp, // current server time
            out T before,
            out T after,
            out double t)     // interpolation factor
        {
            before = default;
            after  = default;
            t = 0;

            // can't sample an empty history
            // interpolation needs at least two entries.
            //   can't Lerp(A, A, 1.5). dist(A, A) * 1.5 is always 0.
            if (history.Count < 2) {
                return false;
            }

            // older than oldest
            if (timestamp < history.Keys[0]) {
                return false;
            }

            // iterate through the history
            // TODO this needs to be faster than O(N)
            //      search around that area.
            //      should be O(1) most of the time, unless sampling was off.
            KeyValuePair<double, T> prev = new KeyValuePair<double, T>();
            foreach (KeyValuePair<double, T> entry in history) {
                // exact match?
                if (timestamp == entry.Key) {
                    before = entry.Value;
                    after = entry.Value;
                    t = Mathd.InverseLerp(entry.Key, entry.Key, timestamp);
                    return true;
                }

                // did we check beyond timestamp? then return the previous two.
                if (entry.Key > timestamp) {
                    before = prev.Value;
                    after = entry.Value;
                    t = Mathd.InverseLerp(prev.Key, entry.Key, timestamp);
                    return true;
                }

                // remember the last
                prev = entry;
            }

            return false;
        }

        // when receiving a correction from the server, we want to insert it
        // into the client's state history.
        // -> if there's already a state at timestamp, replace
        // -> otherwise insert and adjust the next state's delta
        // TODO test coverage
        public static void InsertCorrection<T>(
            SortedList<double, T> stateHistory,
            int stateHistoryLimit,
            T corrected, // corrected state with timestamp
            T before,    // state in history before the correction
            T after)     // state in history after the correction
            where T: PredictedState
        {
            // respect the limit
            // TODO unit test to check if it respects max size
            if (stateHistory.Count >= stateHistoryLimit)
                stateHistory.RemoveAt(0);

            // insert the corrected state into the history, or overwrite if already exists
            stateHistory[corrected.timestamp] = corrected;

            // the entry behind the inserted one still has the delta from (before, after).
            // we need to correct it to (corrected, after).
            //
            // for example:
            //   before:    (t=1.0, delta=10, position=10)
            //   after:     (t=3.0, delta=20, position=30)
            //
            // then we insert:
            //   corrected: (t=2.5, delta=__, position=25)
            //
            // previous delta was from t=1.0 to t=3.0 => 2.0
            // inserted delta is from t=2.5 to t=3.0 => 0.5
            // multiplier is 0.5 / 2.0 = 0.25
            // multiply 'after.delta(20)' by 0.25 to get the new 'after.delta(5)
            //
            // so the new history is:
            //   before:    (t=1.0, delta=10, position=10)
            //   corrected: (t=2.5, delta=__, position=25)
            //   after:     (t=3.0, delta= 5, position=__)
            //
            // so when we apply the correction, the new after.position would be:
            //   corrected.position(25) + after.delta(5) = 30
            //
            double previousDeltaTime = after.timestamp - before.timestamp;     // 3.0 - 1.0 = 2.0
            double correctedDeltaTime = after.timestamp - corrected.timestamp; // 3.0 - 2.5 = 0.5
            double multiplier = correctedDeltaTime / previousDeltaTime;        // 0.5 / 2.0 = 0.25

            // recalculate 'after.delta' with the multiplier
            after.AdjustDeltas((float)multiplier);

            // write the adjusted 'after' value into the history buffer
            stateHistory[after.timestamp] = after;
        }
    }
}
