// comparer to sort NetworkIdentity observing list by distance to main player
// for LocalWorldState.
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    // needs to be a class to avoid allocations here:
    // - List.Sort(IComparer) would box if it was a struct.
    // - we can modify .position every time and reuse the object for sorting.
    //
    // IMPORTANT: make sure to clear
    public class NetworkIdentitySorter : IComparer<NetworkIdentity>
    {
        // store value types for sorting.
        // storing NetworkIdentity would be a bit risky:
        // - it could become null
        // - for whatever reason, .position / .netId may change while sorting,
        //   leaving the SortedSet in corrupted state.
        // => easier to test with value types too!
        Vector3 position;
        uint netId;

        // sorter position needs to be changeable from the outside.
        public void Reset(Vector3 playerPosition, uint playerNetId)
        {
            position = playerPosition;
            netId = playerNetId;
        }

        public int Compare(NetworkIdentity a, NetworkIdentity b)
        {
            // IMPORTANT
            // guarantee the player is ALWAYS included (=highest priority).
            // otherwise if >N players are all at the exact same position
            // (i.e. a spawn point), we would fall back to sorting by netId.
            // and if they all have netIds smaller than the player, then
            // LocalWorldState would NOT include the player itself.
            // => need to guarantee the player can not be despawned by others!
            // => see test: PlayerAlwaysHighestPriority()
            if (a.netId == netId) return -1; // a < b if b is player
            if (b.netId == netId) return 1;  // a > b if b is player

            // otherwise compare distance as usual
            float aDistance = Vector3.Distance(a.transform.position, position);
            float bDistance = Vector3.Distance(b.transform.position, position);

            // compare float distance and return int
            int compared = Comparer<float>.Default.Compare(aDistance, bDistance);

            // IMPORTANT
            // SortedSet does NOT allow two entries with same order.
            // one entry would be discarded automatically.
            // but we DO need to allow two NetworkIdentities at the same DISTANCE.
            // => note that identities with different POSITIONS can still have
            //    the same DISTANCE. so we always need to check DISTANCE!
            // => see test: SamePosition_NotDiscarded()

            // if equal, fall back to comparing hash code.
            // NOT .netId. that's 0 for some unspawned identities, would cause
            //     issues in tests AND require only allowing != 0 netIds.
            //     otherwise two unspawned identities would be equal (both 0).
            // NOTE: if comparing with self, still allow '0' to guarantee that
            //       only one entry stays in SortedSet
            if (compared == 0)
                return Comparer<int>.Default.Compare(a.GetHashCode(), b.GetHashCode());

            return compared;
        }
    }
}
