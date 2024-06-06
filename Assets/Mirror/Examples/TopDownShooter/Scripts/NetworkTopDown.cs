using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class NetworkTopDown : NetworkBehaviour
{
    public CanvasTopDown canvasTopDown;

    public GameObject[] enemyPrefabs;
    public Vector2 enemySpawnRangeX;
    public Vector2 enemySpawnRangeZ;
    private int enemyCounter = 0;

    public override void OnStartServer()
    {
        // spawn one enemy on start of game, then let host spawn more via button
        // for more enemies when using dedicated server, you will need additional logic depending on your game, as it cannot press UI button.
        SpawnEnemy();
    }

    public void SpawnEnemy()
    {
        if (isServer == false)
        {
            print("Only server can spawn enemies, or clients via cmd request.");
        }
        else
        {
            // select random enemy prefab if we have one
            GameObject enemy = Instantiate(enemyPrefabs[Random.Range(0, enemyPrefabs.Length)]);
            // set random spawn position depending on our ranges set via inspector
            enemy.transform.position = new Vector3(Random.Range(enemySpawnRangeX.x, enemySpawnRangeX.y), 0, Random.Range(enemySpawnRangeZ.x, enemySpawnRangeZ.y));
            // network spawn enemy to current and new players
            NetworkServer.Spawn(enemy);

            // update UI
            enemyCounter += 1;
            canvasTopDown.UpdateEnemyUI(enemyCounter);
        }
    }
}
