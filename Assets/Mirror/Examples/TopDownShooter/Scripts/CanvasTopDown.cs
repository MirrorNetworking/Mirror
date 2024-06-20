using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.TopDownShooter
{
    public class CanvasTopDown : MonoBehaviour
    {
        public NetworkTopDown networkTopDown;
        public PlayerTopDown playerTopDown; // This is automatically set by local players script

        public Button buttonSpawnEnemy, buttonRespawnPlayer;
        public Text textEnemies, textKills;

        public GameObject shotMarker;
        public GameObject deathSplatter;
        public AudioSource soundGameIntro, soundGameLoop, soundButtonUI;


#if !UNITY_SERVER
        private void Start()
        {
            buttonSpawnEnemy.onClick.AddListener(ButtonSpawnEnemy);
            buttonRespawnPlayer.onClick.AddListener(ButtonRespawnPlayer);

            StartCoroutine(BGSound());
        }
#endif

        private void ButtonSpawnEnemy()
        {
#if !UNITY_SERVER
            PlaySoundButtonUI();
            networkTopDown.SpawnEnemy();
#endif
        }

        private void ButtonRespawnPlayer()
        {
#if !UNITY_SERVER
            PlaySoundButtonUI();
            playerTopDown.CmdRespawnPlayer();
#endif
        }

        public void UpdateEnemyUI(int value)
        {
#if !UNITY_SERVER
            textEnemies.text = "Enemies: " + value;
#endif
        }

        public void UpdateKillsUI(int value)
        {
#if !UNITY_SERVER
            textKills.text = "Kills: " + value;
#endif
        }

        public void ResetUI()
        {
#if !UNITY_SERVER
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
#endif
        }

#if !UNITY_SERVER
        IEnumerator BGSound()
        {
            soundGameIntro.Play();
            yield return new WaitForSeconds(4.1f);
            soundGameLoop.Play();

        }
#endif

        public void PlaySoundButtonUI()
        {
#if !UNITY_SERVER
            soundButtonUI.Play();
#endif
        }
    }
}