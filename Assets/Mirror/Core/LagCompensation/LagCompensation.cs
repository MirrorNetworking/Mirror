// standalone lag compensation algorithm
// based on the Valve Networking Model:
// https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking
using System.Collections.Generic;

namespace Mirror
{
    public static class LagCompensation
    {
        // history is of <timestamp, capture>.
        // Queue allows for fast 'remove first' and 'append last'.
        //
        // make sure to always insert in order.
        // inserting out of order like [1,2,4,3] would cause issues.
        // can't safeguard this because Queue doesn't have .Last access.
        public static void Insert<T>(
            Queue<KeyValuePair<double, T>> history,
            int historyLimit,
            double timestamp,
            T capture)
            where T : Capture
        {
            // make space according to history limit.
            // do this before inserting, to avoid resizing past capacity.
            if (history.Count >= historyLimit)
                history.Dequeue();

            // insert
            history.Enqueue(new KeyValuePair<double, T>(timestamp, capture));
        }

        // get the two snapshots closest to a given timestamp.
        // those can be used to interpolate the exact snapshot at that time.
        // if timestamp is newer than the newest history entry, then we extrapolate.
        //   't' will be between 1 and 2, before is second last, after is last.
        //   callers should Lerp(before, after, t=1.5) to extrapolate the hit.
        //   see comments below for extrapolation.
        public static bool Sample<T>(
            Queue<KeyValuePair<double, T>> history,
            double timestamp, // current server time
            double interval,  // capture interval
            out T before,
            out T after,
            out double t)     // interpolation factor
            where T : Capture
        {
            before = default;
            after  = default;
            t = 0;

            // can't sample an empty history
            // interpolation needs at least one entry.
            // extrapolation needs at least two entries.
            //   can't Lerp(A, A, 1.5). dist(A, A) * 1.5 is always 0.
            if(history.Count < 2) {
                return false;
            }

            // older than oldest
            if (timestamp < history.Peek().Key) {
                return false;
            }

            // iterate through the history
            // TODO faster version: guess start index by how many 'intervals' we are behind.
            //      search around that area.
            //      should be O(1) most of the time, unless sampling was off.
            KeyValuePair<double, T> prev = new KeyValuePair<double, T>();
            KeyValuePair<double, T> prevPrev = new KeyValuePair<double, T>();
            foreach(KeyValuePair<double, T> entry in history) {
                // exact match?
                if (timestamp == entry.Key) {
                    before = entry.Value;
                    after = entry.Value;
                    t = Mathd.InverseLerp(before.timestamp, after.timestamp, timestamp);
                    return true;
                }

                // did we check beyond timestamp? then return the previous two.
                if (entry.Key > timestamp) {
                    before = prev.Value;
                    after = entry.Value;
                    t = Mathd.InverseLerp(before.timestamp, after.timestamp, timestamp);
                    return true;
                }

                // remember the last two for extrapolation.
                // Queue doesn't have access to .Last.
                prevPrev = prev;
                prev = entry;
            }

            // newer than newest: extrapolate up to one interval.
            // let's say we capture every 100 ms:
            // 100, 200, 300, 400
            // and the server is at 499
            // if a client sends CmdFire at time 480, then there's no history entry.
            // => adding the current entry every time would be too expensive.
            //    worst case we would capture at 401, 402, 403, 404, ... 100 times
            // => not extrapolating isn't great. low latency clients would be
            //    punished by missing their targets since no entry at 'time' was found.
            // => extrapolation is the best solution. make sure this works as
            //    expected and within limits.
            if (prev.Key < timestamp && timestamp <= prev.Key + interval) {
                // return the last two valid snapshots.
                // can't just return (after, after) because we can't extrapolate
                // if their distance is 0.
                before = prevPrev.Value;
                after = prev.Value;

                // InverseLerp will give [after, after+interval].
                // but we return [before, after, t].
                // so add +1 for the distance from before->after
                t = 1 + Mathd.InverseLerp(after.timestamp, after.timestamp + interval, timestamp);
                return true;
            }

            return false;
        }

        // never trust the client.
        // we estimate when a message was sent.
        // don't trust the client to tell us the time.
        //   https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking
        //   Command Execution Time = Current Server Time - Packet Latency - Client View Interpolation
        // => lag compensation demo estimation is off by only ~6ms
        public static double EstimateTime(double serverTime, double rtt, double bufferTime)
        {
            // packet latency is one trip from client to server, so rtt / 2
            // client view interpolation is the snapshot interpolation buffer time
            double latency = rtt / 2;
            return serverTime - latency - bufferTime;
        }

        // convenience function to draw all history gizmos.
        // this should be called from OnDrawGizmos.
        public static void DrawGizmos<T>(Queue<KeyValuePair<double, T>> history)
            where T : Capture
        {
            foreach (KeyValuePair<double, T> entry in history)
                entry.Value.DrawGizmo();
        }
    }
}
