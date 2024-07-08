using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Mirror;

namespace Mirror.Examples.PhysicsPickupParty
{
    public class TeamManager : NetworkBehaviour
    {
        public SceneReference sceneReference;

        //public List<int> teamID = new List<int>() { 0, 1, 2, 3 };
        public List<Color32> teamColours = new List<Color32>() { new Color32(255, 209, 220, 255), new Color32(253, 253, 150, 255), new Color32(193, 225, 193, 255), new Color32(174, 198, 207, 255) };
        public List<int> teamCurrentPlayers = new List<int>() { 0, 0, 0, 0 };

        public void AddPlayerTeamList(PlayerPickupParty _playerPickupParty)
        {
            print("AddPlayerTeamList");

            int minIndex = teamCurrentPlayers.IndexOf(teamCurrentPlayers.Min());
            teamCurrentPlayers[minIndex] = teamCurrentPlayers[minIndex] + 1;
            _playerPickupParty.teamID = minIndex;
        }

        public void RemovePlayerTeamList(int _team)
        {
            print("RemovePlayerTeamList: " + _team);

            teamCurrentPlayers[_team] = teamCurrentPlayers[_team] - 1;
        }

        // lazily sync var this, its only once per second, nothing intense like a constantly changing float
        [SyncVar(hook = nameof(OnGameStartTimerChanged))]
        public int gameStartTime = 60;
        private int gameStartTimeOriginal;

        public override void OnStartServer()
        {
            gameStartTimeOriginal = gameStartTime;
            StartCoroutine(GameStartTimerCountdown());
        }

        private IEnumerator GameStartTimerCountdown()
        {
            while (gameStartTime > 0)
            {
                yield return new WaitForSeconds(1.0f);
                gameStartTime -= 1;
            }
        }

        public void OnGameStartTimerChanged(int _old, int _new)
        {
            sceneReference.gameStartTimer.text = gameStartTime + "s";

            if (gameStartTime <= 0)
            {
                sceneReference.panelControls.SetActive(false);
                sceneReference.panelInfo.SetActive(false);
                sceneReference.panelGameStartTimer.SetActive(false);
                sceneReference.panelRoundEndTimer.SetActive(true);
            }
        }
    }
}