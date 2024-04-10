using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/ Interest Management/ Match/Match Interest Management")]
    public class MatchInterestManagement : InterestManagement
    {
        [Header("Diagnostics")]
        [ReadOnly, SerializeField]
        internal ushort matchCount;

        readonly Dictionary<Guid, HashSet<NetworkMatch>> matchObjects =
            new Dictionary<Guid, HashSet<NetworkMatch>>();

        readonly HashSet<Guid> dirtyMatches = new HashSet<Guid>();

        // LateUpdate so that all spawns/despawns/changes are done
        [ServerCallback]
        void LateUpdate()
        {
            // Rebuild all dirty matches
            // dirtyMatches will be empty if no matches changed members
            // by spawning or destroying or changing matchId in this frame.
            foreach (Guid dirtyMatch in dirtyMatches)
            {
                // rebuild always, even if matchObjects[dirtyMatch] is empty.
                // Players might have left the match, but they may still be spawned.
                RebuildMatchObservers(dirtyMatch);

                // clean up empty entries in the dict
                if (matchObjects[dirtyMatch].Count == 0)
                    matchObjects.Remove(dirtyMatch);
            }

            dirtyMatches.Clear();

            matchCount = (ushort)matchObjects.Count;
        }

        [ServerCallback]
        void RebuildMatchObservers(Guid matchId)
        {
            foreach (NetworkMatch networkMatch in matchObjects[matchId])
                if (networkMatch.netIdentity != null)
                    NetworkServer.RebuildObservers(networkMatch.netIdentity, false);
        }

        // called by NetworkMatch.matchId setter
        [ServerCallback]
        internal void OnMatchChanged(NetworkMatch networkMatch, Guid oldMatch)
        {
            // This object is in a new match so observers in the prior match
            // and the new match need to rebuild their respective observers lists.

            // Remove this object from the hashset of the match it just left
            // Guid.Empty is never a valid matchId
            if (oldMatch != Guid.Empty)
            {
                dirtyMatches.Add(oldMatch);
                matchObjects[oldMatch].Remove(networkMatch);
            }

            // Guid.Empty is never a valid matchId
            if (networkMatch.matchId == Guid.Empty)
                return;

            dirtyMatches.Add(networkMatch.matchId);

            // Make sure this new match is in the dictionary
            if (!matchObjects.ContainsKey(networkMatch.matchId))
                matchObjects[networkMatch.matchId] = new HashSet<NetworkMatch>();

            // Add this object to the hashset of the new match
            matchObjects[networkMatch.matchId].Add(networkMatch);
        }

        [ServerCallback]
        public override void OnSpawned(NetworkIdentity identity)
        {
            if (!identity.TryGetComponent(out NetworkMatch networkMatch))
                return;

            Guid networkMatchId = networkMatch.matchId;

            // Guid.Empty is never a valid matchId...do not add to matchObjects collection
            if (networkMatchId == Guid.Empty)
                return;

            // Debug.Log($"MatchInterestManagement.OnSpawned({identity.name}) currentMatch: {currentMatch}");
            if (!matchObjects.TryGetValue(networkMatchId, out HashSet<NetworkMatch> objects))
            {
                objects = new HashSet<NetworkMatch>();
                matchObjects.Add(networkMatchId, objects);
            }

            objects.Add(networkMatch);

            // Match ID could have been set in NetworkBehaviour::OnStartServer on this object.
            // Since that's after OnCheckObserver is called it would be missed, so force Rebuild here.
            // Add the current match to dirtyMatches for LateUpdate to rebuild it.
            dirtyMatches.Add(networkMatchId);
        }

        [ServerCallback]
        public override void OnDestroyed(NetworkIdentity identity)
        {
            // Don't RebuildSceneObservers here - that will happen in LateUpdate.
            // Multiple objects could be destroyed in same frame and we don't
            // want to rebuild for each one...let LateUpdate do it once.
            // We must add the current match to dirtyMatches for LateUpdate to rebuild it.
            if (identity.TryGetComponent(out NetworkMatch currentMatch))
            {
                if (currentMatch.matchId != Guid.Empty &&
                    matchObjects.TryGetValue(currentMatch.matchId, out HashSet<NetworkMatch> objects) &&
                    objects.Remove(currentMatch))
                    dirtyMatches.Add(currentMatch.matchId);
            }
        }

        [ServerCallback]
        public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver)
        {
            // Never observed if no NetworkMatch component
            if (!identity.TryGetComponent(out NetworkMatch identityNetworkMatch))
                return false;

            // Guid.Empty is never a valid matchId
            if (identityNetworkMatch.matchId == Guid.Empty)
                return false;

            // Never observed if no NetworkMatch component
            if (!newObserver.identity.TryGetComponent(out NetworkMatch newObserverNetworkMatch))
                return false;

            // Guid.Empty is never a valid matchId
            if (newObserverNetworkMatch.matchId == Guid.Empty)
                return false;

            return identityNetworkMatch.matchId == newObserverNetworkMatch.matchId;
        }

        [ServerCallback]
        public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers)
        {
            if (!identity.TryGetComponent(out NetworkMatch networkMatch))
                return;

            // Guid.Empty is never a valid matchId
            if (networkMatch.matchId == Guid.Empty)
                return;

            // Abort if this match hasn't been created yet by OnSpawned or OnMatchChanged
            if (!matchObjects.TryGetValue(networkMatch.matchId, out HashSet<NetworkMatch> objects))
                return;

            // Add everything in the hashset for this object's current match
            foreach (NetworkMatch netMatch in objects)
                if (netMatch.netIdentity != null && netMatch.netIdentity.connectionToClient != null)
                    newObservers.Add(netMatch.netIdentity.connectionToClient);
        }
    }
}
