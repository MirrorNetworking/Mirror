using System;

#if ENABLE_UNET
namespace UnityEngine.Networking
{
    // This class represents the player entity in a network game, there can be multiple players per client
    // when there are multiple people playing on one machine
    // The server has one connection per client, and the connection has the player instances of that client
    // The client has player instances as member variables (should this be removed and just go though the connection like the server does?)
    public class PlayerController
    {
        internal const short kMaxLocalPlayers = 8;

        public short playerControllerId = -1;
        public NetworkIdentity unetView;
        public GameObject gameObject;

        public const int MaxPlayersPerClient = 32;

        public PlayerController()
        {
        }

        public bool IsValid { get { return playerControllerId != -1; } }

        internal PlayerController(GameObject go, short playerControllerId)
        {
            gameObject = go;
            unetView = go.GetComponent<NetworkIdentity>();
            this.playerControllerId = playerControllerId;
        }

        public override string ToString()
        {
            return string.Format("ID={0} NetworkIdentity NetID={1} Player={2}", new object[] { playerControllerId, (unetView != null ? unetView.netId.ToString() : "null"), (gameObject != null ? gameObject.name : "null") });
        }
    }
}
#endif //ENABLE_UNET
