using System;
using System.Linq;
using UnityEngine;

namespace Mirror.Examples.NetworkRoomCanvas
{
    [AddComponentMenu("")]
    public class NetworkRoomManagerExample : NetworkRoomManager
    {
        /// <summary>
        /// Called when players leaves or joins the room
        /// </summary>
        public event Action<NetworkRoomPlayerExample[]> onPlayerListChanged;

        /// <summary>
        /// Called on the server when all players are ready
        /// </summary>
        public event Action<bool> onServerAllPlayersReady;

        public NetworkRoomPlayerExample[] GetPlayers()
        {
            // casts roomSlots to NetworkRoomPlayerExample and returns array
            return roomSlots.Select(player => (NetworkRoomPlayerExample)player).ToArray();
        }

        public override void OnRoomClientEnter()
        {
            onPlayerListChanged?.Invoke(GetPlayers());
        }

        public override void OnRoomClientExit()
        {
            onPlayerListChanged?.Invoke(GetPlayers());
        }


        public override void OnRoomServerPlayersReady()
        {
            onServerAllPlayersReady?.Invoke(true);
        }

        public override void OnRoomServerPlayersNotReady()
        {
            onServerAllPlayersReady?.Invoke(false);
        }


        /* ******** Below is same as Room Example ******** */
        /// <inheritdoc/>
        public override bool OnRoomServerSceneLoadedForPlayer(NetworkConnection conn, GameObject roomPlayer, GameObject gamePlayer)
        {
            PlayerScore playerScore = gamePlayer.GetComponent<PlayerScore>();
            playerScore.index = roomPlayer.GetComponent<NetworkRoomPlayerExample>().index;
            return true;
        }
    }
}
