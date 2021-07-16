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

        HashSet<Guid> dirtyMatches = new HashSet<Guid>();

        public override void OnSpawned(NetworkIdentity identity)
        {
            Guid currentMatch = identity.GetComponent<NetworkMatch>().matchId;
            lastObjectMatch[identity] = currentMatch;
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
            Guid currentMatch = lastObjectMatch[identity];
            lastObjectMatch.Remove(identity);
            if (matchObjects.TryGetValue(currentMatch, out HashSet<NetworkIdentity> objects) && objects.Remove(identity))
                RebuildMatchObservers(currentMatch);
        }

        void Update()
        {
            // only on server
            if (!NetworkServer.active) return;

            // for each spawned:
            //   if match changed:
            //     add previous to dirty
            //     add new to dirty
            foreach (NetworkIdentity netIdentity in NetworkIdentity.spawned.Values)
            {
                Guid currentMatch = lastObjectMatch[netIdentity];
                Guid newMatch = netIdentity.GetComponent<NetworkMatch>().matchId;
                if (newMatch == currentMatch) continue;

                // Mark new/old scenes as dirty so they get rebuilt
                dirtyMatches.Add(currentMatch);
                dirtyMatches.Add(newMatch);

                // This object is in a new match so observers in the prior scene
                // and the new scene need to rebuild their respective observers lists.

                // Remove this object from the hashset of the scene it just left
                matchObjects[currentMatch].Remove(netIdentity);

                // Set this to the new match this object just entered
                lastObjectMatch[netIdentity] = newMatch;

                // Make sure this new match is in the dictionary
                if (!matchObjects.ContainsKey(newMatch))
                    matchObjects.Add(newMatch, new HashSet<NetworkIdentity>());

                // Add this object to the hashset of the new match
                matchObjects[newMatch].Add(netIdentity);
            }

            // rebuild all dirty matchs
            foreach (Guid dirtyMatch in dirtyMatches)
            {
                RebuildMatchObservers(dirtyMatch);
            }

            dirtyMatches.Clear();
        }

        void RebuildMatchObservers(Guid matchId)
        {
            foreach (NetworkIdentity netIdentity in matchObjects[matchId])
                if (netIdentity != null)
                    NetworkServer.RebuildObservers(netIdentity, false);
        }

        public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnection newObserver)
        {
            return identity.GetComponent<NetworkMatch>().matchId ==
                   newObserver.identity.GetComponent<NetworkMatch>().matchId;
        }

        public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnection> newObservers,
            bool initialize)
        {
            Guid matchId = identity.GetComponent<NetworkMatch>().matchId;
            if (!matchObjects.TryGetValue(matchId, out HashSet<NetworkIdentity> objects))
                return;

            // Add everything in the hashset for this object's current scene
            foreach (NetworkIdentity networkIdentity in objects)
                if (networkIdentity != null && networkIdentity.connectionToClient != null)
                    newObservers.Add(networkIdentity.connectionToClient);
        }
    }
}
