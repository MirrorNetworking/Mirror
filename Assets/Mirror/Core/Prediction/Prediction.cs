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

        Vector3 velocity { get; set; }
        Vector3 velocityDelta { get; set; }

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
            out int afterIndex, // so corrections can skip the entries before
            out double t)     // interpolation factor
        {
            before = default;
            after  = default;
            afterIndex = -1;
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

            // iterate through the history, starting at 1 so we can always have 'prev'
            for (int i = 1; i < history.Count; ++i)
            {
                double previousTimestamp = history.Keys[i-1];
                double entryTimestamp = history.Keys[i];
                T prev = history.Values[i-1];
                T entry = history.Values[i];

                // exact match?
                if (entryTimestamp == timestamp) {
                    before = entry;
                    after = entry;
                    afterIndex = i;
                    t = Mathd.InverseLerp(entryTimestamp, entryTimestamp, timestamp);
                    return true;
                }

                // did we check beyond timestamp? then return the previous two.
                if (entryTimestamp > timestamp) {
                    before = prev;
                    after = entry;
                    afterIndex = i;
                    t = Mathd.InverseLerp(previousTimestamp, entryTimestamp, timestamp);
                    return true;
                }
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

            // fix multiplier becoming NaN if previousDeltaTime is 0:
            // double multiplier = correctedDeltaTime / previousDeltaTime;
            double multiplier = previousDeltaTime != 0 ? correctedDeltaTime / previousDeltaTime : 0; // 0.5 / 2.0 = 0.25

            // recalculate 'after.delta' with the multiplier
            after.AdjustDeltas((float)multiplier);

            // write the adjusted 'after' value into the history buffer
            stateHistory[after.timestamp] = after;
        }

        // client may need to correct parts of the history after receiving server state.
        // CorrectHistory inserts the correction at[i], then corrects [i..n].
        // in other words, it inserts the absolute value and reapplies the deltas
        // that the client moved since then.
        public static T CorrectHistory<T>(
            SortedList<double, T> stateHistory,
            T corrected,                        // corrected state with timestamp
            out int correctedAmount)            // for debugging
            where T: PredictedState
        {
            // now go through the history:
            // 1. skip all states before the inserted / corrected entry
            // 3. apply all deltas after timestamp
            // 4. recalculate corrected position based on inserted + sum(deltas)
            // 5. apply rigidbody correction
            T last = corrected;
            correctedAmount = 0; // for debugging
            for (int i = 0; i < stateHistory.Count; ++i)
            {
                double key = stateHistory.Keys[i];
                T entry = stateHistory.Values[i];

                // skip all states before (and including) the corrected entry
                //
                // ideally InsertCorrection() above should return the inserted
                // index to skip faster. but SortedList.Insert doesn't return an
                // index. would need binary search.
                if (key <= corrected.timestamp)
                    continue;

                // this state is after the inserted state.
                // correct it's absolute position based on last + delta.
                entry.position = last.position + entry.positionDelta;
                // TODO rotation
                entry.velocity = last.velocity + entry.velocityDelta;

                // save the corrected entry into history.
                // if we don't, then corrections for [i+1] would compare the
                // uncorrected state and attempt to correct again, resulting in
                // noticeable jitter and displacements.
                //
                // not saving it would also result in objects flying towards
                // infinity when using sendInterval = 0.
                stateHistory[key] = entry;

                // debug draw the corrected state
                // Debug.DrawLine(last.position, entry.position, Color.cyan, lineTime);

                // save last
                last = entry;
                correctedAmount += 1;
            }

            // return the recomputed state after all deltas were applied to the correction
            return last;
        }
    }
}
