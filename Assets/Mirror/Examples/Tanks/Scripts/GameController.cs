using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.Tanks
{
    public class GameController : MonoBehaviour
    {
        public int minimumPlayersToStartGame;

        public Tank LocalPlayer;
        public GameObject RespawnButton;
        public GameObject HealthTextLabel;
        public GameObject ScoreTextLabel;
        public Text HealthText;
        public Text ScoreText;

        void Update()
        {
            FindLocalTank();
            UpdateStats();
            CheckIfTankIsDead();
        }

        void FindLocalTank()
        {
            if (LocalPlayer != null)
                return;

            if (!NetworkManager.singleton.isNetworkActive)
                return;

            if (ClientScene.localPlayer == null)
                return;

            LocalPlayer = ClientScene.localPlayer.GetComponent<Tank>();

            HealthTextLabel.SetActive(true);
            ScoreTextLabel.SetActive(true);
        }

        void UpdateStats()
        {
            if (LocalPlayer == null)
                return;

            HealthText.text = LocalPlayer.health.ToString();
            ScoreText.text = LocalPlayer.score.ToString();
        }

        void CheckIfTankIsDead()
        {
            if (LocalPlayer == null)
                return;

            if(LocalPlayer.isDead && LocalPlayer.lives > 0)
            {
                RespawnButton.SetActive(true);
            }
            else
            {
                RespawnButton.SetActive(false);
            }
        }

        public void RespawnButtonHandler()
        {
            LocalPlayer.RespawnButtonHandler();
        }
    }
}
