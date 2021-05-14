using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    // Comparer for our observing sorted set
    internal class ObservingComparer : IComparer<NetworkIdentity>
    {
        // need to know the main player object's position
        NetworkConnection owner;
        internal ObservingComparer(NetworkConnection owner)
        {
            this.owner = owner;
        }

        public int Compare(NetworkIdentity a, NetworkIdentity b)
        {
            // does owner connection have a player?
            if (owner.identity != null)
            {
                // calculate both identity's distances to the player
                Vector3 ownerPosition = owner.identity.transform.position;
                float distanceA = Vector3.Distance(ownerPosition, a.transform.position);
                float distanceB = Vector3.Distance(ownerPosition, b.transform.position);
                return Comparer<float>.Default.Compare(distanceA, distanceB);
            }
            return 0;
        }
    }
}
