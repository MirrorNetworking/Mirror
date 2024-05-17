using System.Linq;
using UnityEngine;

namespace Mirror.Examples.TankTheftAuto
{
    [AddComponentMenu("")]
    public class AuthorityNetworkManager : NetworkManager
    {
        /// <summary>
        /// Called on the server when a client disconnects.
        /// <para>This is called on the Server when a Client disconnects from the Server. Use an override to decide what should happen when a disconnection is detected.</para>
        /// </summary>
        /// <param name="conn">Connection from client.</param>
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // this code is to reset any objects belonging to disconnected clients
            // make a copy because the original collection will change in the loop
            NetworkIdentity[] copyOfOwnedObjects = conn.owned.ToArray();
            // Loop the copy, skipping the player object.
            // RemoveClientAuthority on everything else
            foreach (NetworkIdentity identity in copyOfOwnedObjects)
            {
                if (identity != conn.identity)
                    identity.RemoveClientAuthority();
            }

            base.OnServerDisconnect(conn);
        }
    }
}
