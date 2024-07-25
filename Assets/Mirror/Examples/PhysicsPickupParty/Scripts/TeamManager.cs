using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Mirror;
using UnityEngine.SceneManagement;

namespace Mirror.Examples.PhysicsPickupParty
{
    public class TeamManager : NetworkBehaviour
    {
        public SceneReference sceneReference;

        //public List<int> teamID = new List<int>() { 0, 1, 2, 3 };
        public List<Color32> teamColours = new List<Color32>();// { new Color32(255, 209, 220, 255), new Color32(253, 253, 150, 255), new Color32(193, 225, 193, 255), new Color32(174, 198, 207, 255) };
        public List<int> teamCurrentPlayers = new List<int>();// { 0, 0, 0, 0 };
        public List<Transform> teamSpawnAreas = new List<Transform>();
        public int teamSpawnRange = 5;

        public int restartRoundTime = 5;

        public void AddPlayerTeamList(PlayerPickupParty _playerPickupParty)
        {
           // print("AddPlayerTeamList");

            int minIndex = teamCurrentPlayers.IndexOf(teamCurrentPlayers.Min());
            teamCurrentPlayers[minIndex] = teamCurrentPlayers[minIndex] + 1;
            _playerPickupParty.teamID = minIndex;
        }

        public void RemovePlayerTeamList(int _team)
        {
            //print("RemovePlayerTeamList: " + _team);

            teamCurrentPlayers[_team] = teamCurrentPlayers[_team] - 1;
        }

        // lazily sync var this, its only once per second, nothing intense like a constantly changing float
        [SyncVar(hook = nameof(OnGameStartTimerChanged))]
        public int gameStartTime = 60;
        private int gameStartTimeOriginal;

        // 0 game waiting, no player movement
        // 1 started, players can move
        // 2 game end, show results, disable player movement
        [SyncVar(hook = nameof(OnGameStatusChanged))]
        public int gameStatus = -1;

        public override void OnStartServer()
        {
            gameStatus = 0;
            StartCoroutine(GameStartTimerCountdown());

            sceneReference.skipGameStartTimerObj.SetActive(true);
        }

        private IEnumerator GameStartTimerCountdown()
        {
            while (gameStartTime > 0)
            {
                gameStartTime -= 1;
                yield return new WaitForSeconds(1.0f);
            }
            if (gameStartTime <= 0)
            {
                gameStatus = 1;
                StartCoroutine(RoundEndTimerCountdown());
                StartCoroutine(sceneReference.pickupManager.StartPickupInterval());
            }
        }

        public void OnGameStartTimerChanged(int _old, int _new)
        {
            if (gameStartTime > 0)
            {
                sceneReference.gameStartTimer.text = gameStartTime + "s";
            }
            else
            {
                sceneReference.gameStartTimer.text = "Go!";
                sceneReference.playerPickupParty.cameraFollow.enabled = true;
                sceneReference.startGameSound.Play();

                sceneReference.BGSoundGameplay.Play();
                sceneReference.BGSoundWaiting.Stop();
            }
        }

        public void OnGameStatusChanged(int _old, int _new)
        {
            if (gameStatus == 0)
            {
                sceneReference.panelControls.SetActive(true);
                sceneReference.panelInfo.SetActive(true);
                sceneReference.panelGameStartTimer.SetActive(true);
                sceneReference.panelRoundEndTimer.SetActive(false);
            }
            else if (gameStatus == 1)
            {
                sceneReference.panelControls.SetActive(false);
                sceneReference.panelInfo.SetActive(false);
                sceneReference.panelGameStartTimer.SetActive(false);
                sceneReference.panelRoundEndTimer.SetActive(true);
            }
            else if (gameStatus == 2)
            {
                sceneReference.panelControls.SetActive(true);
                sceneReference.panelInfo.SetActive(true);
                sceneReference.panelGameStartTimer.SetActive(false);
                sceneReference.panelRoundEndTimer.SetActive(false);
                sceneReference.EndGameUI();
            }
        }

        // lazily sync var this, its only once per second, nothing intense like a constantly changing float
        [SyncVar(hook = nameof(OnRoundEndTimerChanged))]
        public int roundEndTime = 60;
        private int roundEndTimeOriginal;

        public void OnRoundEndTimerChanged(int _old, int _new)
        {
            sceneReference.roundEndTimer.text = roundEndTime + "s";
            if (roundEndTime <= 0)
            {
                sceneReference.startGameSound.Play();

                sceneReference.BGSoundGameplay.Stop();
                sceneReference.BGSoundWaiting.volume = 0.3f;
                sceneReference.BGSoundWaiting.Play();
            }
        }

        private IEnumerator RoundEndTimerCountdown()
        {
            while (roundEndTime > 0)
            {
                roundEndTime -= 1;
                yield return new WaitForSeconds(1.0f);
            }
            if (roundEndTime <= 0)
            {
                gameStatus = 2;
                StartCoroutine(RestartRoundCountdown());
                sceneReference.pickupManager.currentSpawns = sceneReference.pickupManager.maxSpawns;
            }
        }

        public void SetPlayerSpawnPoint(int _teamID, PlayerPickupParty _playerPickupParty)
        {
            _playerPickupParty.transform.position = new Vector3(
                Random.Range(teamSpawnAreas[_teamID].position.x - teamSpawnRange, teamSpawnAreas[_teamID].position.x + teamSpawnRange),
                 teamSpawnAreas[_teamID].position.y,
                Random.Range(teamSpawnAreas[_teamID].position.z - teamSpawnRange, teamSpawnAreas[_teamID].position.z + teamSpawnRange));
        }

        private IEnumerator RestartRoundCountdown()
        {
            while (restartRoundTime > 0)
            {
                restartRoundTime -= 1;
                yield return new WaitForSeconds(1.0f);
            }
            if (restartRoundTime <= 0)
            {
                NetworkManager.singleton.ServerChangeScene(SceneManager.GetActiveScene().name);
            }
        }
    }
}