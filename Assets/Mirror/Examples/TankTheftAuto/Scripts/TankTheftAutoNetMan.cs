using Mirror.Examples.Common.Controllers.Tank;
using UnityEngine;

namespace Mirror.Examples.TankTheftAuto
{
    [AddComponentMenu("")]
    public class TankTheftAutoNetMan : NetworkManager
    {
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // If the client was driving a tank, destroy the cached player object
            if (conn.authenticationData is GameObject player)
                NetworkServer.Destroy(player);

            if (conn.identity != null)
            {
                if (conn.identity.TryGetComponent(out TankTurretBase tankTurret))
                    tankTurret.playerColor = Color.black;

                if (conn.identity.TryGetComponent(out TankAuthority tankAuthority))
                {
                    tankAuthority.isControlled = false;
                    NetworkServer.RemovePlayerForConnection(conn, RemovePlayerOptions.KeepActive);
                }
            }

            base.OnServerDisconnect(conn);
        }
    }
}
