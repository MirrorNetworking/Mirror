using UnityEngine;
using Mirror.Components.NetworkLobby;

namespace Mirror.Examples.NetworkLobby
{
    public class NetworkLobbyManagerExt : NetworkLobbyManager
    {
        public override bool OnLobbyServerSceneLoadedForPlayer(GameObject lobbyPlayer, GameObject gamePlayer)
        {
            PlayerController player = gamePlayer.GetComponent<PlayerController>();
            player.Index = lobbyPlayer.GetComponent<NetworkLobbyPlayer>().Index;
            player.playerColor = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
            return true;
        }
    }
}
