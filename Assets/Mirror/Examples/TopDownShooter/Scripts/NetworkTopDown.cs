using UnityEngine;
using Mirror;

namespace Mirror.Examples.TopDownShooter
{
    public class NetworkTopDown : NetworkBehaviour
    {
        public CanvasTopDown canvasTopDown;

        // Have as many enemy variations as you want, remember to set them in NetworkManagers Registered Spawnable Prefabs array.
        public GameObject[] enemyPrefabs;
        // For our square map with no obstacles, we'l just set a range, for your own game, you may have set spawn points
        public Vector2 enemySpawnRangeX;
        public Vector2 enemySpawnRangeZ;

        [SyncVar(hook = nameof(OnEnemyCounterChanged))]
        public int enemyCounter = 0;

        public override void OnStartServer()
        {
#if !UNITY_SERVER
            canvasTopDown.ResetUI();
#endif
            // Spawn one enemy on start of game, then let player host spawn more via button
            SpawnEnemy();
        }

#if !UNITY_SERVER
        public override void OnStartClient()
        {
            canvasTopDown.ResetUI();
        }
#endif

        [ServerCallback]
        public void SpawnEnemy()
        {
            if (isServer == false)
            {
                print("Only server can spawn enemies, or clients via cmd request.");
            }
            else
            {
                // Select random enemy prefab if we have more than one
                GameObject enemy = Instantiate(enemyPrefabs[Random.Range(0, enemyPrefabs.Length)]);
                // Set random spawn position depending on our ranges set via inspector
                enemy.transform.position = new Vector3(Random.Range(enemySpawnRangeX.x, enemySpawnRangeX.y), 0, Random.Range(enemySpawnRangeZ.x, enemySpawnRangeZ.y));
                // Network spawn enemy to current and new players
                NetworkServer.Spawn(enemy);
                enemyCounter += 1;
#if !UNITY_SERVER
                // update UI
                canvasTopDown.UpdateEnemyUI(enemyCounter);
#endif
            }
        }

        void OnEnemyCounterChanged(int _Old, int _New)
        {
#if !UNITY_SERVER
            canvasTopDown.UpdateEnemyUI(enemyCounter);
#endif
        }
        
    }
}