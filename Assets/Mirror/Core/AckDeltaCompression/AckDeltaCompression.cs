// all the unreliable ack delta compression magic in one place.
// everything static with simple data types to allow for full & easy test coverage.

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
        // when sending a batch, tracks the connection's identities that were in
        // that batch @ timestamp.
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

        // when receiving an ack from a connection, update latest ack for
        // all networkidentities that were in the acked batch.
        internal static void UpdateIdentityAcks(
            double timestamp,
            SortedList<double, HashSet<uint>> identityTicks,
            Dictionary<uint, double> identityAcks)
        {
            // find the identities that were in the acked batch
            if (!identityTicks.TryGetValue(timestamp, out HashSet<uint> identities))
            {
                // if the timestamp was not in history, there could be multiple reasons.
                // find out why exactly it happens and produce a useful(!) log message.

                // empty history:
                if (identityTicks.Count == 0)
                {
                    Debug.Log($"UpdateLatestAck: batch @ {timestamp:F3} was not in empty history. This can happen for batches during login / auth.");
                }
                // non emtpty
                else
                {
                    double oldest = identityTicks.Keys[0];
                    double newest = identityTicks.Keys[identityTicks.Count - 1];

                    // older than oldest?
                    if (timestamp < oldest)
                    {
                        Debug.Log($"UpdateLatestAck: batch @ {timestamp:F3} was older than history of size {identityTicks.Count} from {oldest:F3}..{newest:F3}.");
                    }
                    // newer than newest?
                    else if (timestamp > newest)
                    {
                        Debug.Log($"UpdateLatestAck: batch @ {timestamp:F3} was newer than history of size {identityTicks.Count} from {oldest:F3}..{newest:F3}.");
                    }
                    // inbetween but couldn't sample?
                    else
                    {
                        Debug.Log($"UpdateLatestAck: batch @ {timestamp:F3} was inbetween history of size {identityTicks.Count} from {oldest:F3}..{newest:F3} but couldn't sample.");
                    }
                }
                return;
            }

            // update latest acks for all identities that were in the batch
            foreach (uint netId in identities)
            {
                // unreliable messages may arrive out of order.
                // only update if newer.
                if (!identityAcks.TryGetValue(netId, out double ackTimestamp) || timestamp > ackTimestamp)
                    identityAcks[netId] = timestamp;
            }
        }
    }
}
