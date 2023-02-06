using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/ Interest Management/ Match/Match Interest Management")]
    public class MatchInterestManagement : InterestManagement
    {
        readonly Dictionary<Guid, HashSet<NetworkIdentity>> matchObjects =
            new Dictionary<Guid, HashSet<NetworkIdentity>>();

        readonly Dictionary<NetworkIdentity, Guid> lastObjectMatch =
            new Dictionary<NetworkIdentity, Guid>();

        readonly HashSet<Guid> dirtyMatches = new HashSet<Guid>();

        [ServerCallback]
        public override void OnSpawned(NetworkIdentity identity)
        {
            if (!identity.TryGetComponent(out NetworkMatch networkMatch))
                return;

            Guid networkMatchId = networkMatch.matchId;
            lastObjectMatch[identity] = networkMatchId;

            // Guid.Empty is never a valid matchId...do not add to matchObjects collection
            if (networkMatchId == Guid.Empty)
                return;

            // Debug.Log($"MatchInterestManagement.OnSpawned({identity.name}) currentMatch: {currentMatch}");
            if (!matchObjects.TryGetValue(networkMatchId, out HashSet<NetworkIdentity> objects))
            {
                objects = new HashSet<NetworkIdentity>();
                matchObjects.Add(networkMatchId, objects);
            }

            objects.Add(identity);

            // Match ID could have been set in NetworkBehaviour::OnStartServer on this object.
            // Since that's after OnCheckObserver is called it would be missed, so force Rebuild here.
            RebuildMatchObservers(networkMatchId);
        }

        [ServerCallback]
        public override void OnDestroyed(NetworkIdentity identity)
        {
            if (lastObjectMatch.TryGetValue(identity, out Guid currentMatch))
            {
                lastObjectMatch.Remove(identity);
                if (currentMatch != Guid.Empty && matchObjects.TryGetValue(currentMatch, out HashSet<NetworkIdentity> objects) && objects.Remove(identity))
                    RebuildMatchObservers(currentMatch);
            }
        }

        // internal so we can update from tests
        [ServerCallback]
        internal void Update()
        {
            // for each spawned:
            //   if match changed:
            //     add previous to dirty
            //     add new to dirty
            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                // Ignore objects that don't have a NetworkMatch component
                if (!identity.TryGetComponent(out NetworkMatch networkMatch))
                    continue;

                Guid newMatch = networkMatch.matchId;
                if (!lastObjectMatch.TryGetValue(identity, out Guid currentMatch))
                    continue;

                // Guid.Empty is never a valid matchId
                // Nothing to do if matchId hasn't changed
                if (newMatch == Guid.Empty || newMatch == currentMatch)
                    continue;

                // Mark new/old matches as dirty so they get rebuilt
                UpdateDirtyMatches(newMatch, currentMatch);

                // This object is in a new match so observers in the prior match
                // and the new match need to rebuild their respective observers lists.
                UpdateMatchObjects(identity, newMatch, currentMatch);
            }

            // rebuild all dirty matches
            foreach (Guid dirtyMatch in dirtyMatches)
                RebuildMatchObservers(dirtyMatch);

            dirtyMatches.Clear();
        }

        void UpdateDirtyMatches(Guid newMatch, Guid currentMatch)
        {
            // Guid.Empty is never a valid matchId
            if (currentMatch != Guid.Empty)
                dirtyMatches.Add(currentMatch);

            dirtyMatches.Add(newMatch);
        }

        void UpdateMatchObjects(NetworkIdentity netIdentity, Guid newMatch, Guid currentMatch)
        {
            // Remove this object from the hashset of the match it just left
            // Guid.Empty is never a valid matchId
            if (currentMatch != Guid.Empty)
                matchObjects[currentMatch].Remove(netIdentity);

            // Set this to the new match this object just entered
            lastObjectMatch[netIdentity] = newMatch;

            // Make sure this new match is in the dictionary
            if (!matchObjects.ContainsKey(newMatch))
                matchObjects.Add(newMatch, new HashSet<NetworkIdentity>());

            // Add this object to the hashset of the new match
            matchObjects[newMatch].Add(netIdentity);
        }

        void RebuildMatchObservers(Guid matchId)
        {
            foreach (NetworkIdentity netIdentity in matchObjects[matchId])
                if (netIdentity != null)
                    NetworkServer.RebuildObservers(netIdentity, false);
        }

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

        public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers)
        {
            if (!identity.TryGetComponent(out NetworkMatch networkMatch))
                return;

            Guid matchId = networkMatch.matchId;

            // Guid.Empty is never a valid matchId
            if (matchId == Guid.Empty)
                return;

            if (!matchObjects.TryGetValue(matchId, out HashSet<NetworkIdentity> objects))
                return;

            // Add everything in the hashset for this object's current match
            foreach (NetworkIdentity networkIdentity in objects)
                if (networkIdentity != null && networkIdentity.connectionToClient != null)
                    newObservers.Add(networkIdentity.connectionToClient);
        }
    }
}
