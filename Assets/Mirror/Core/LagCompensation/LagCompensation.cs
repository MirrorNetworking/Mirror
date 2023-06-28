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
    }
}
