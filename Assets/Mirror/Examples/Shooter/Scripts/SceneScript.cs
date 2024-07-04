using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mirror.Examples.Shooter
{
    public class SceneScript : MonoBehaviour
    {
        // A non networked script for references
        public Button buttonExit, buttonRespawn, buttonBot, buttonScores;
        public Text textHealth, textAmmo;
        public GameObject respawnUI, weaponCamera, characterDataPrefab;
        public ShooterCharacterData characterData;
        public Player localPlayer;
        public PlayerScores playerScores;

        private void Awake()
        {
            characterData = Instantiate(characterDataPrefab).GetComponent<ShooterCharacterData>();
        }

        private void Start()
        {
            buttonExit.onClick.AddListener(ButtonExit);
            buttonRespawn.onClick.AddListener(ButtonRespawn);
            buttonBot.onClick.AddListener(ButtonBot);
            buttonScores.onClick.AddListener(ButtonScores);
        }

        public void ButtonExit()
        {
            //Debug.Log("ButtonExit");
            NetworkManager.singleton.StopHost();
            //Application.Quit();
        }

        public void ButtonRespawn()
        {
            //Debug.Log("ButtonRespawn");
            localPlayer.CmdReqestPlayerStatusChange(0);
        }

        public void ButtonBot()
        {
            if (NetworkServer.active)
            {
                Debug.Log("ButtonBot");
                GameObject botObj = Instantiate(NetworkManager.singleton.playerPrefab, NetworkManager.startPositions[UnityEngine.Random.Range(0, NetworkManager.startPositions.Count)].position, NetworkManager.startPositions[UnityEngine.Random.Range(0, NetworkManager.startPositions.Count)].rotation);
                botObj.AddComponent<Bot>();
                NetworkServer.Spawn(botObj);
            }
            else
            {
                Debug.Log("ButtonBot can only be called on Server/Host");
            }
        }

        public void ButtonScores()
        {
            if (playerScores.playerScores.activeSelf)
            {
                playerScores.ButtonClose();
            }
            else
            {
                playerScores.ButtonOpen();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ButtonScores();
            }
        }
    }
}
