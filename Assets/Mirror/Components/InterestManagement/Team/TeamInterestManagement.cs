using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/ Interest Management/ Team/Team Interest Management")]
    public class TeamInterestManagement : InterestManagement
    {
        readonly Dictionary<string, HashSet<NetworkTeam>> teamObjects =
            new Dictionary<string, HashSet<NetworkTeam>>();

        readonly HashSet<string> dirtyTeams = new HashSet<string>();

        // LateUpdate so that all spawns/despawns/changes are done
        [ServerCallback]
        void LateUpdate()
        {
            // Rebuild all dirty teams
            // dirtyTeams will be empty if no teams changed members
            // by spawning or destroying or changing teamId in this frame.
            foreach (string dirtyTeam in dirtyTeams)
            {
                // rebuild always, even if teamObjects[dirtyTeam] is empty.
                // Players might have left the team, but they may still be spawned.
                RebuildTeamObservers(dirtyTeam);

                // clean up empty entries in the dict
                if (teamObjects[dirtyTeam].Count == 0)
                    teamObjects.Remove(dirtyTeam);
            }

            dirtyTeams.Clear();
        }

        [ServerCallback]
        void RebuildTeamObservers(string teamId)
        {
            foreach (NetworkTeam networkTeam in teamObjects[teamId])
                if (networkTeam.netIdentity != null)
                    NetworkServer.RebuildObservers(networkTeam.netIdentity, false);
        }

        // called by NetworkTeam.teamId setter
        [ServerCallback]
        internal void OnTeamChanged(NetworkTeam networkTeam, string oldTeam)
        {
            // This object is in a new team so observers in the prior team
            // and the new team need to rebuild their respective observers lists.

            // Remove this object from the hashset of the team it just left
            // Null / Empty string is never a valid teamId
            if (!string.IsNullOrWhiteSpace(oldTeam))
            {
                dirtyTeams.Add(oldTeam);
                teamObjects[oldTeam].Remove(networkTeam);
            }

            // Null / Empty string is never a valid teamId
            if (string.IsNullOrWhiteSpace(networkTeam.teamId))
                return;

            dirtyTeams.Add(networkTeam.teamId);

            // Make sure this new team is in the dictionary
            if (!teamObjects.ContainsKey(networkTeam.teamId))
                teamObjects[networkTeam.teamId] = new HashSet<NetworkTeam>();

            // Add this object to the hashset of the new team
            teamObjects[networkTeam.teamId].Add(networkTeam);
        }

        [ServerCallback]
        public override void OnSpawned(NetworkIdentity identity)
        {
            if (!identity.TryGetComponent(out NetworkTeam networkTeam))
                return;

            string networkTeamId = networkTeam.teamId;

            // Null / Empty string is never a valid teamId...do not add to teamObjects collection
            if (string.IsNullOrWhiteSpace(networkTeamId))
                return;

            // Debug.Log($"TeamInterestManagement.OnSpawned({identity.name}) currentTeam: {currentTeam}");
            if (!teamObjects.TryGetValue(networkTeamId, out HashSet<NetworkTeam> objects))
            {
                objects = new HashSet<NetworkTeam>();
                teamObjects.Add(networkTeamId, objects);
            }

            objects.Add(networkTeam);

            // Team ID could have been set in NetworkBehaviour::OnStartServer on this object.
            // Since that's after OnCheckObserver is called it would be missed, so force Rebuild here.
            // Add the current team to dirtyTeames for LateUpdate to rebuild it.
            dirtyTeams.Add(networkTeamId);
        }

        [ServerCallback]
        public override void OnDestroyed(NetworkIdentity identity)
        {
            // Don't RebuildSceneObservers here - that will happen in LateUpdate.
            // Multiple objects could be destroyed in same frame and we don't
            // want to rebuild for each one...let LateUpdate do it once.
            // We must add the current team to dirtyTeames for LateUpdate to rebuild it.
            if (identity.TryGetComponent(out NetworkTeam currentTeam))
            {
                if (!string.IsNullOrWhiteSpace(currentTeam.teamId) &&
                    teamObjects.TryGetValue(currentTeam.teamId, out HashSet<NetworkTeam> objects) &&
                    objects.Remove(currentTeam))
                    dirtyTeams.Add(currentTeam.teamId);
            }
        }

        public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver)
        {
            // Always observed if no NetworkTeam component
            if (!identity.TryGetComponent(out NetworkTeam identityNetworkTeam))
                return true;

            if (identityNetworkTeam.forceShown)
                return true;

            // Null / Empty string is never a valid teamId
            if (string.IsNullOrWhiteSpace(identityNetworkTeam.teamId))
                return false;

            // Always observed if no NetworkTeam component
            if (!newObserver.identity.TryGetComponent(out NetworkTeam newObserverNetworkTeam))
                return true;

            // Null / Empty string is never a valid teamId
            if (string.IsNullOrWhiteSpace(newObserverNetworkTeam.teamId))
                return false;

            //Debug.Log($"TeamInterestManagement.OnCheckObserver {identity.name} {identityNetworkTeam.teamId} | {newObserver.identity.name} {newObserverNetworkTeam.teamId}");

            // Observed only if teamId's team
            return identityNetworkTeam.teamId == newObserverNetworkTeam.teamId;
        }

        public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers)
        {
            // If this object doesn't have a NetworkTeam then it's visible to all clients
            if (!identity.TryGetComponent(out NetworkTeam networkTeam))
            {
                AddAllConnections(newObservers);
                return;
            }

            // If this object has NetworkTeam and forceShown == true then it's visible to all clients
            if (networkTeam.forceShown)
            {
                AddAllConnections(newObservers);
                return;
            }

            // Null / Empty string is never a valid teamId
            if (string.IsNullOrWhiteSpace(networkTeam.teamId))
                return;

            // Abort if this team hasn't been created yet by OnSpawned or OnTeamChanged
            if (!teamObjects.TryGetValue(networkTeam.teamId, out HashSet<NetworkTeam> objects))
                return;

            // Add everything in the hashset for this object's current team
            foreach (NetworkTeam netTeam in objects)
                if (netTeam.netIdentity != null && netTeam.netIdentity.connectionToClient != null)
                    newObservers.Add(netTeam.netIdentity.connectionToClient);
        }

        void AddAllConnections(HashSet<NetworkConnectionToClient> newObservers)
        {
            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                // authenticated and joined world with a player?
                if (conn != null && conn.isAuthenticated && conn.identity != null)
                    newObservers.Add(conn);
            }
        }
    }
}
