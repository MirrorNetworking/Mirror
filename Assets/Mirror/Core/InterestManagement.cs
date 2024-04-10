// interest management component for custom solutions like
// distance based, spatial hashing, raycast based, etc.

using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    [HelpURL("https://mirror-networking.gitbook.io/docs/guides/interest-management")]
    public abstract class InterestManagement : InterestManagementBase
    {
        // allocate newObservers helper HashSet
        readonly HashSet<NetworkConnectionToClient> newObservers =
            new HashSet<NetworkConnectionToClient>();

        // rebuild observers for the given NetworkIdentity.
        // Server will automatically spawn/despawn added/removed ones.
        //   newObservers: cached hashset to put the result into
        //   initialize: true if being rebuilt for the first time
        //
        // IMPORTANT:
        // => global rebuild would be more simple, BUT
        // => local rebuild is way faster for spawn/despawn because we can
        //    simply rebuild a select NetworkIdentity only
        // => having both .observers and .observing is necessary for local
        //    rebuilds
        //
        // in other words, this is the perfect solution even though it's not
        // completely simple (due to .observers & .observing).
        //
        // Mirror maintains .observing automatically in the background. best of
        // both worlds without any worrying now!
        public abstract void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers);

        // helper function to trigger a full rebuild.
        // most implementations should call this in a certain interval.
        // some might call this all the time, or only on team changes or
        // scene changes and so on.
        //
        // IMPORTANT: check if NetworkServer.active when using Update()!
        [ServerCallback]
        protected void RebuildAll()
        {
            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                NetworkServer.RebuildObservers(identity, false);
            }
        }

        public override void Rebuild(NetworkIdentity identity, bool initialize)
        {
            // clear newObservers hashset before using it
            newObservers.Clear();

            // not force hidden?
            if (identity.visibility != Visibility.ForceHidden)
            {
                OnRebuildObservers(identity, newObservers);
            }

            // IMPORTANT: AFTER rebuilding add own player connection in any case
            // to ensure player always sees himself no matter what.
            // -> OnRebuildObservers might clear observers, so we need to add
            //    the player's own connection AFTER. 100% fail safe.
            // -> fixes https://github.com/vis2k/Mirror/issues/692 where a
            //    player might teleport out of the ProximityChecker's cast,
            //    losing the own connection as observer.
            if (identity.connectionToClient != null)
            {
                newObservers.Add(identity.connectionToClient);
            }

            bool changed = false;

            // add all newObservers that aren't in .observers yet
            foreach (NetworkConnectionToClient conn in newObservers)
            {
                // only add ready connections.
                // otherwise the player might not be in the world yet or anymore
                if (conn != null && conn.isReady)
                {
                    if (initialize || !identity.observers.ContainsKey(conn.connectionId))
                    {
                        // new observer
                        conn.AddToObserving(identity);
                        // Debug.Log($"New Observer for {gameObject} {conn}");
                        changed = true;
                    }
                }
            }

            // remove all old .observers that aren't in newObservers anymore
            foreach (NetworkConnectionToClient conn in identity.observers.Values)
            {
                if (!newObservers.Contains(conn))
                {
                    // removed observer
                    conn.RemoveFromObserving(identity, false);
                    // Debug.Log($"Removed Observer for {gameObject} {conn}");
                    changed = true;
                }
            }

            // copy new observers to observers
            if (changed)
            {
                identity.observers.Clear();
                foreach (NetworkConnectionToClient conn in newObservers)
                {
                    if (conn != null && conn.isReady)
                        identity.observers.Add(conn.connectionId, conn);
                }
            }

            // special case for host mode: we use SetHostVisibility to hide
            // NetworkIdentities that aren't in observer range from host.
            // this is what games like Dota/Counter-Strike do too, where a host
            // does NOT see all players by default. they are in memory, but
            // hidden to the host player.
            //
            // this code is from UNET, it's a bit strange but it works:
            // * it hides newly connected identities in host mode
            //   => that part was the intended behaviour
            // * it hides ALL NetworkIdentities in host mode when the host
            //   connects but hasn't selected a character yet
            //   => this only works because we have no .localConnection != null
            //      check. at this stage, localConnection is null because
            //      StartHost starts the server first, then calls this code,
            //      then starts the client and sets .localConnection. so we can
            //      NOT add a null check without breaking host visibility here.
            // * it hides ALL NetworkIdentities in server-only mode because
            //   observers never contain the 'null' .localConnection
            //   => that was not intended, but let's keep it as it is so we
            //      don't break anything in host mode. it's way easier than
            //      iterating all identities in a special function in StartHost.
            if (initialize)
            {
                if (!newObservers.Contains(NetworkServer.localConnection))
                {
                    SetHostVisibility(identity, false);
                }
            }
        }
    }
}
