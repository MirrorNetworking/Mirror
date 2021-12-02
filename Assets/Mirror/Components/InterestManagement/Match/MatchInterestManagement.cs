using System;
using System.Collections.Generic;

namespace Mirror
{
    public class MatchInterestManagement : InterestManagement
    {
        readonly Dictionary<Guid, HashSet<NetworkIdentity>> matchObjects =
            new Dictionary<Guid, HashSet<NetworkIdentity>>();

        readonly Dictionary<NetworkIdentity, Guid> lastObjectMatch =
            new Dictionary<NetworkIdentity, Guid>();

        readonly HashSet<Guid> dirtyMatches = new HashSet<Guid>();

        public override void OnSpawned(NetworkIdentity identity)
        {
            if (!identity.TryGetComponent<NetworkMatch>(out NetworkMatch networkMatch))
                return;

            Guid currentMatch = networkMatch.matchId;
            lastObjectMatch[identity] = currentMatch;

            // Guid.Empty is never a valid matchId...do not add to matchObjects collection
            if (currentMatch == Guid.Empty)
                return;

            // Debug.Log($"MatchInterestManagement.OnSpawned({identity.name}) currentMatch: {currentMatch}");
            if (!matchObjects.TryGetValue(currentMatch, out HashSet<NetworkIdentity> objects))
            {
                objects = new HashSet<NetworkIdentity>();
                matchObjects.Add(currentMatch, objects);
            }

            objects.Add(identity);
        }

        public override void OnDestroyed(NetworkIdentity identity)
        {
            lastObjectMatch.TryGetValue(identity, out Guid currentMatch);
            lastObjectMatch.Remove(identity);
            if (currentMatch != Guid.Empty && matchObjects.TryGetValue(currentMatch, out HashSet<NetworkIdentity> objects) && objects.Remove(identity))
                RebuildMatchObservers(currentMatch);
        }

        // internal so we can update from tests
        [ServerCallback]
        internal void Update()
        {
            // for each spawned:
            //   if match changed:
            //     add previous to dirty
            //     add new to dirty
            foreach (NetworkIdentity netIdentity in NetworkServer.spawned.Values)
            {
                // Ignore objects that don't have a NetworkMatch component
                if (!netIdentity.TryGetComponent<NetworkMatch>(out NetworkMatch networkMatch))
                    continue;

                Guid newMatch = networkMatch.matchId;
                lastObjectMatch.TryGetValue(netIdentity, out Guid currentMatch);

                // Guid.Empty is never a valid matchId
                // Nothing to do if matchId hasn't changed
                if (newMatch == Guid.Empty || newMatch == currentMatch)
                    continue;

                // Mark new/old matches as dirty so they get rebuilt
                UpdateDirtyMatches(newMatch, currentMatch);

                // This object is in a new match so observers in the prior match
                // and the new match need to rebuild their respective observers lists.
                UpdateMatchObjects(netIdentity, newMatch, currentMatch);
            }

            // rebuild all dirty matchs
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

        public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnection newObserver)
        {
            if (!identity.TryGetComponent<NetworkMatch>(out NetworkMatch identityNetworkMatch))
                return false;

            if (!newObserver.identity.TryGetComponent<NetworkMatch>(out NetworkMatch newObserverNetworkMatch))
                return false;

            return identityNetworkMatch.matchId == newObserverNetworkMatch.matchId;
        }

        public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnection> newObservers, bool initialize)
        {
            if (!identity.TryGetComponent<NetworkMatch>(out NetworkMatch networkMatch))
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
