using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.TopDownShooter
{
    public class CanvasTopDown : MonoBehaviour
    {
        public NetworkTopDown networkTopDown;
        public PlayerTopDown playerTopDown;

        public Button buttonSpawnEnemy, buttonRespawnPlayer;
        public Text textEnemies, textKills;

        public GameObject shotMarker;
        public GameObject deathSplatter;

        public AudioSource soundGameIntro, soundGameLoop, soundButtonUI;

        private void Start()
        {
            buttonSpawnEnemy.onClick.AddListener(ButtonSpawnEnemy);
            buttonRespawnPlayer.onClick.AddListener(ButtonRespawnPlayer);

            StartCoroutine(BGSound());
        }

        private void ButtonSpawnEnemy()
        {
            PlaySoundButtonUI();
            networkTopDown.SpawnEnemy();
        }

        private void ButtonRespawnPlayer()
        {
            PlaySoundButtonUI();
            playerTopDown.CmdRespawnPlayer();
        }

        public void UpdateEnemyUI(int value)
        {
            textEnemies.text = "Enemies: " + value;
        }

        public void UpdateKillsUI(int value)
        {
            textKills.text = "Kills: " + value;
        }

        public void ResetUI()
        {
            if (NetworkServer.active)
            {
                buttonSpawnEnemy.gameObject.SetActive(true);
            }
            else
            {
                buttonSpawnEnemy.gameObject.SetActive(false);
            }

            buttonRespawnPlayer.gameObject.SetActive(false);
            shotMarker.SetActive(false);
            textEnemies.text = "Enemies: 0";
            textKills.text = "Kills: 0";
        }

        IEnumerator BGSound()
        {
            soundGameIntro.Play();
            yield return new WaitForSeconds(4.1f);
            soundGameLoop.Play();
        }

        public void PlaySoundButtonUI()
        {
            soundButtonUI.Play();
        }
    }
}