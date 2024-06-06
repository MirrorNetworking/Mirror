using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CanvasTopDown : MonoBehaviour
{
    public NetworkTopDown networkTopDown;

    public Button buttonSpawnEnemy;
    public Text textEnemies, textKills;

    public GameObject shotMarker;

    private void Start()
    {
        buttonSpawnEnemy.onClick.AddListener(ButtonSpawnEnemy);
    }

    private void ButtonSpawnEnemy()
    {
        networkTopDown.SpawnEnemy();
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
