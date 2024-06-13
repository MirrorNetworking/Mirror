using UnityEngine;
using Mirror;

namespace Mirror.Examples.TopDownShooter
{
    public class NetworkTopDown : NetworkBehaviour
    {
        public CanvasTopDown canvasTopDown;

        public GameObject[] enemyPrefabs;
        public Vector2 enemySpawnRangeX;
        public Vector2 enemySpawnRangeZ;

        [SyncVar(hook = nameof(OnEnemyCounterChanged))]
        public int enemyCounter = 0;

        public override void OnStartServer()
        {
            canvasTopDown.ResetUI();
            // spawn one enemy on start of game, then let host spawn more via button
            // for more enemies when using dedicated server, you will need additional logic depending on your game, as it cannot press UI button.
            SpawnEnemy();
        }

        public override void OnStartClient()
        {
            canvasTopDown.ResetUI();
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

        void OnEnemyCounterChanged(int _Old, int _New)
        {
            canvasTopDown.UpdateEnemyUI(enemyCounter);
        }
        
    }
}