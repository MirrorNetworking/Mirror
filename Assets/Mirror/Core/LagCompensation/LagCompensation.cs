// standalone lag compensation algorithm
// based on the Valve Networking Model:
// https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking
using System.Collections.Generic;

namespace Mirror
{
    public static class LagCompensation
    {
        // TODO ringbuffer
        // history is of <timestamp, capture>
        public static void Insert<T>(
            List<KeyValuePair<double, T>> history,
            int historyLimit,
            double timestamp,
            T capture)
            where T : Capture
        {
            // insert
            history.Add(new KeyValuePair<double, T>(timestamp, capture));

            // make space according to history limit
            if (history.Count > historyLimit)
                history.RemoveAt(0);
        }

        // get the two snapshots closest to a given timestamp.
        // those can be used to interpolate the exact snapshot at that time.
        // TODO better data structure for faster lookup
        public static bool Sample<T>(
            List<KeyValuePair<double, T>> history,
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
            if (timestamp < history[0].Key) {
                return false;
            }

            // iterate through the history
            for (int i = 0; i < history.Count; i++) {
                // exact match?
                if (timestamp == history[i].Key) {
                    before = history[i].Value;
                    after = history[i].Value;
                    t = Mathd.InverseLerp(before.timestamp, after.timestamp, timestamp);
                    return true;
                }

                // did we check beyond timestamp? then return the previous two.
                if (history[i].Key > timestamp) {
                    before = history[i-1].Value;
                    after = history[i].Value;
                    t = Mathd.InverseLerp(before.timestamp, after.timestamp, timestamp);
                    return true;
                }
            }

            // newer than newest
            return false;
        }

        // never trust the client.
        // we estimate when a message was sent.
        // don't trust the client to tell us the time.
        // https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking
        // Command Execution Time = Current Server Time - Packet Latency - Client View Interpolation
        public static double EstimateTime(double serverTime, double rtt, double bufferTime)
        {
            // packet latency is one trip from client to server, so rtt / 2
            // client view interpolation is the snapshot interpolation buffer time
            double latency = rtt / 2;
            return serverTime - latency - bufferTime;
        }
    }
}
