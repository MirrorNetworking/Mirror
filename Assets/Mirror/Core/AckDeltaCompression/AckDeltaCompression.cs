// all the unreliable ack delta compression magic in one place.
// everything static with simple data types to allow for full & easy test coverage.

using System.Collections.Generic;
using System.Linq;

namespace Mirror
{
    public static class AckDeltaCompression
    {
        // NetworkBehaviour aggregated dirty bits //////////////////////////////
        // insert current dirty bits and aggregate into each history entry.
        internal static void InsertAndAggregate(
            SortedList<double, (ulong, ulong)> history,
            double timestamp,
            ulong syncVarDirtyBits,
            ulong syncObjectDirtyBits,
            int historyCount)
        {
            // limit max count.
            // we are going to insert one, so remove if >=.
            if (history.Count >= historyCount)
                history.RemoveAt(0);

            // aggregate current dirty bits into each history entry immediately.
            // this is faster than doing it on demand per-connection:
            // O(historyCount) is faster than O(connections) * O(historyCount)
            // for-int because foreach wouldn't allow modifications while iterating.
            for (int i = 0; i < history.Count; ++i)
            {
                double entryTimestamp = history.Keys[i];
                (ulong entrySyncVarBits, ulong entrySyncObjectBits) = history.Values[i];

                // aggregate dirty bits
                ulong newSyncVarDirtyBits = entrySyncVarBits | syncVarDirtyBits;
                ulong newSyncObjectDirtyBits = entrySyncObjectBits | syncObjectDirtyBits;

                // save the changes
                history[entryTimestamp] = (newSyncVarDirtyBits, newSyncObjectDirtyBits);
            }

            // insert the most recent dirty bits
            history[timestamp] = (syncVarDirtyBits, syncObjectDirtyBits);
        }

        // NetworkConnection keeping track of NetworkIdentity acks /////////////
        internal static void TrackIdentityAtTick(
            double timestamp,
            uint netId,
            SortedList<double, HashSet<uint>> identityTicks,
            int historyCount)
        {
            // insert timestamp if not exists
            if (!identityTicks.ContainsKey(timestamp))
            {
                // limit max count.
                // we are going to insert one, so remove if >=.
                // TODO reuse the HashSets instead of removing & reallocating.
                if (identityTicks.Count >= historyCount)
                    identityTicks.RemoveAt(0);

                // allocate a new HashSet for netids
                identityTicks[timestamp] = new HashSet<uint>();
            }

            // get the hashset for this tick
            HashSet<uint> netIds = identityTicks[timestamp];

            // add the netid to the hashset
            netIds.Add(netId);
        }
    }
}
