using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.Tanks
{
    public class TankGameManager : MonoBehaviour
    {
        public int MinimumPlayersForGame = 1;

        public Tank LocalPlayer;
        public GameObject StartPanel;
        public GameObject GameOverPanel;
        public GameObject HealthTextLabel;
        public GameObject ScoreTextLabel;
        public Text HealthText;
        public Text ScoreText;
        public Text PlayerNameText;
        public Text WinnerNameText;
        public bool IsGameReady;
        public bool IsGameOver;
        public List<Tank> players = new List<Tank>();
        public NetworkManager NetworkManager;


        void Update()
        {
            if (NetworkManager.IsNetworkActive)
            {
                GameReadyCheck();
                GameOverCheck();

                if (LocalPlayer == null)
                {
                    FindLocalTank();
                }
                else
                {
                    ShowReadyMenu();
                    UpdateStats();
                }
            }
            else
            {
                //Cleanup state once network goes offline
                IsGameReady = false;
                LocalPlayer = null;
                players.Clear();
            }
        }

        void ShowReadyMenu()
        {
            if (NetworkManager.client.Active)
            {

                if (LocalPlayer.isReady)
                    return;

                StartPanel.SetActive(true);
            }
        }

        void GameReadyCheck()
        {
            if (!IsGameReady)
            {
                //Look for connections that are not in the player list
                CheckPlayersNotInList();

                //If minimum connections has been check if they are all ready
                if (players.Count >= MinimumPlayersForGame && GetAllReadyState())
                {
                    IsGameReady = true;
                    AllowTankMovement();

                    //Update Local GUI:
                    StartPanel.SetActive(false);
                    HealthTextLabel.SetActive(true);
                    ScoreTextLabel.SetActive(true);
                }
            }
        }

        void CheckPlayersNotInList()
        {
            foreach (KeyValuePair<uint, NetworkIdentity> kvp in NetworkManager.client.Spawned)
            {
                Tank comp = kvp.Value.GetComponent<Tank>();
                if (comp != null && !players.Contains(comp))
                {
                    //Add if new
                    players.Add(comp);
                }
            }
        }

        bool GetAllReadyState()
        {
            bool AllReady = true;
            foreach (Tank tank in players)
            {
                if (!tank.isReady)
                {
                    AllReady = false;
                }
            }
            return AllReady;
        }

        void GameOverCheck()
        {
            if (!IsGameReady)
                return;

            //Cant win a game you play by yourself. But you can still use this example for testing network/movement
            if (players.Count == 1)
                return;

            if (GetAlivePlayerCount() == 1)
            {
                IsGameOver = true;
                GameOverPanel.SetActive(true);
                DisallowTankMovement();
            }
        }

        int GetAlivePlayerCount()
        {
            int alivePlayerCount = 0;
            foreach (Tank tank in players)
            {
                if (!tank.IsDead)
                {
                    alivePlayerCount++;

                    //If there is only 1 player left alive this will end up being their name
                    WinnerNameText.text = tank.playerName;
                }
            }
            return alivePlayerCount;
        }

        void FindLocalTank()
        {
            //Check to see if the player is loaded in yet
            if (NetworkManager.client.LocalPlayer == null)
                return;

            LocalPlayer = NetworkManager.client.LocalPlayer.GetComponent<Tank>();
        }

        void UpdateStats()
        {
            HealthText.text = LocalPlayer.health.ToString();
            ScoreText.text = LocalPlayer.score.ToString();
        }

        public void ReadyButtonHandler()
        {
            LocalPlayer.SendReadyToServer(PlayerNameText.text);
        }

        //All players are ready and game has started. Allow players to move.
        void AllowTankMovement()
        {
            foreach (Tank tank in players)
            {
                tank.allowMovement = true;
            }
        }

        //Game is over. Prevent movement
        void DisallowTankMovement()
        {
            foreach (Tank tank in players)
            {
                tank.allowMovement = false;
            }
        }
    }
}
