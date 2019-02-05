using UnityEngine;
using Mirror.Components.NetworkLobby;

namespace Mirror.Examples.NetworkLobby
{
    public class NetworkLobbyManagerExt : NetworkLobbyManager
    {
        /// <summary>
        /// Called just after GamePlayer object is instantiated and just before it replaces LobbyPlayer object.
        /// This is the ideal point to pass any data like player name, credentials, tokens, colors, etc.
        /// into the GamePlayer object as it is about to enter the Online scene.
        /// </summary>
        /// <param name="lobbyPlayer"></param>
        /// <param name="gamePlayer"></param>
        /// <returns>true unless some code in here decides it needs to abort the replacement</returns>
        public override bool OnLobbyServerSceneLoadedForPlayer(GameObject lobbyPlayer, GameObject gamePlayer)
        {
            PlayerController player = gamePlayer.GetComponent<PlayerController>();
            player.Index = lobbyPlayer.GetComponent<NetworkLobbyPlayer>().Index;
            player.playerColor = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
            return true;
        }

        bool showStartButton = false;

        public override void OnLobbyServerPlayersReady()
        {
            //base.OnLobbyServerPlayersReady();
            showStartButton = true;
        }

        public override void OnGUI()
        {
            base.OnGUI();
            if (allPlayersReady && showStartButton)
            {
                Rect startRect = new Rect(150, 300, 120, 20);
                if (GUI.Button(startRect, "START GAME"))
                {
                    showStartButton = false;
                    ServerChangeScene(GameplayScene);
                }
            }
        }
    }
}
