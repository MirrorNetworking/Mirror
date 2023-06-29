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
            if (history.Count > historyLimit)
                history.Dequeue();

            // insert
            history.Enqueue(new KeyValuePair<double, T>(timestamp, capture));
        }

        // get the two snapshots closest to a given timestamp.
        // those can be used to interpolate the exact snapshot at that time.
        public static bool Sample<T>(
            Queue<KeyValuePair<double, T>> history,
            double timestamp,
            out T before,
            out T after,
            out double t) // interpolation factor
            where T : Capture
        {
            before = default;
            after  = default;
            t = 0;

            // can't sample an empty history
            if(history.Count == 0) {
                return false;
            }

            // older than oldest
            if (timestamp < history.Peek().Key) {
                return false;
            }

            // iterate through the history
            KeyValuePair<double, T> prev = new KeyValuePair<double, T>();
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

                prev = entry;
            }

            // newer than newest
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
    }
}
