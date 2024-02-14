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

        // use Vector3 for both Rigidbody3D and Rigidbody2D, that's fine
        Vector3 position { get; set; }
        Vector3 positionDelta { get; set; }

        Quaternion rotation { get; set; }
        Quaternion rotationDelta { get; set; }

        Vector3 velocity { get; set; }
        Vector3 velocityDelta { get; set; }

        Vector3 angularVelocity { get; set; }
        Vector3 angularVelocityDelta { get; set; }
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
            out int afterIndex,
            out double t)     // interpolation factor
        {
            before = default;
            after  = default;
            t = 0;
            afterIndex = -1;

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
            int index = 0; // manually count when iterating. easier than for-int loop.
            KeyValuePair<double, T> prev = new KeyValuePair<double, T>();
            foreach (KeyValuePair<double, T> entry in history) {
                // exact match?
                if (timestamp == entry.Key)
                {
                    before = entry.Value;
                    after = entry.Value;
                    afterIndex = index;
                    t = Mathd.InverseLerp(entry.Key, entry.Key, timestamp);
                    return true;
                }

                // did we check beyond timestamp? then return the previous two.
                if (entry.Key > timestamp)
                {
                    before = prev.Value;
                    after = entry.Value;
                    afterIndex = index;
                    t = Mathd.InverseLerp(prev.Key, entry.Key, timestamp);
                    return true;
                }

                // remember the last
                prev = entry;
                index += 1;
            }

            return false;
        }

        // inserts a server state into the client's history.
        // readjust the deltas of the states after the inserted one.
        // returns the corrected final position.
        public static T CorrectHistory<T>(
            SortedList<double, T> stateHistory,
            int stateHistoryLimit,
            T corrected,     // corrected state with timestamp
            T before,        // state in history before the correction
            T after,         // state in history after the correction
            int afterIndex) // index of the 'after' value so we don't need to find it again here
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

            // fix multiplier becoming NaN if previousDeltaTime is 0:
            // double multiplier = correctedDeltaTime / previousDeltaTime;
            double multiplier = previousDeltaTime != 0 ? correctedDeltaTime / previousDeltaTime : 0; // 0.5 / 2.0 = 0.25

            // recalculate 'after.delta' with the multiplier
            after.positionDelta        = Vector3.Lerp(Vector3.zero, after.positionDelta, (float)multiplier);
            after.velocityDelta        = Vector3.Lerp(Vector3.zero, after.velocityDelta, (float)multiplier);
            after.angularVelocityDelta = Vector3.Lerp(Vector3.zero, after.angularVelocityDelta, (float)multiplier);
            // Quaternions always need to be normalized in order to be a valid rotation after operations
            after.rotationDelta        = Quaternion.Slerp(Quaternion.identity, after.rotationDelta, (float)multiplier).normalized;

            // changes aren't saved until we overwrite them in the history
            stateHistory[after.timestamp] = after;

            // second step: readjust all absolute values by rewinding client's delta moves on top of it.
            T last = corrected;
            for (int i = afterIndex; i < stateHistory.Count; ++i)
            {
                double key = stateHistory.Keys[i];
                T entry = stateHistory.Values[i];

                // correct absolute position based on last + delta.
                entry.position        = last.position + entry.positionDelta;
                entry.velocity        = last.velocity + entry.velocityDelta;
                entry.angularVelocity = last.angularVelocity + entry.angularVelocityDelta;
                // Quaternions always need to be normalized in order to be a valid rotation after operations
                entry.rotation        = (entry.rotationDelta * last.rotation).normalized; // quaternions add delta by multiplying in this order

                // save the corrected entry into history.
                stateHistory[key] = entry;

                // save last
                last = entry;
            }

            // third step: return the final recomputed state.
            return last;
        }
    }
}
