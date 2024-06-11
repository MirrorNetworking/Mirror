using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CanvasTopDown : MonoBehaviour
{
    public NetworkTopDown networkTopDown;
    public PlayerTopDown playerTopDown;

    public Button buttonSpawnEnemy, buttonRespawnPlayer;
    public Text textEnemies, textKills;

    public GameObject shotMarker;
    public GameObject deathSplatter;

    private void Start()
    {
        buttonSpawnEnemy.onClick.AddListener(ButtonSpawnEnemy);
        buttonRespawnPlayer.onClick.AddListener(ButtonRespawnPlayer);
    }

    private void ButtonSpawnEnemy()
    {
        networkTopDown.SpawnEnemy();
    }

    private void ButtonRespawnPlayer()
    {
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
    
}
