using System;
using UnityEngine;

namespace Mirror.Examples.Hex3D
{
    [AddComponentMenu("")]
    public class Hex3DNetworkManager : NetworkManager
    {
        // Overrides the base singleton so we don't have to cast to this type everywhere.
        public static new Hex3DNetworkManager singleton => (Hex3DNetworkManager)NetworkManager.singleton;

        [Header("Spawns")]
        public GameObject spawnPrefab;

        [Range(1, 8000)]
        public ushort spawnPrefabsCount = 1000;

        [Range(1, 10)]
        public byte spawnPrefabSpacing = 3;

        public override void OnValidate()
        {
            if (Application.isPlaying) return;
            base.OnValidate();

            // Adjust spawnPrefabsCount to have an even cube root
            ushort cubeRoot = (ushort)Mathf.Pow(spawnPrefabsCount, 1f / 3f);
            spawnPrefabsCount = (ushort)(Mathf.Pow(cubeRoot, 3f));
        }

        public override void OnStartClient()
        {
            NetworkClient.RegisterPrefab(spawnPrefab);
        }

        public override void OnStartServer()
        {
            // instantiate an empty GameObject
            GameObject Spawns = new GameObject("Spawns");
            Transform SpawnsTransform = Spawns.transform;

            int spawned = 0;

            // Spawn prefabs in a cube grid centered around origin (0,0,0)
            float cubeRoot = Mathf.Pow(spawnPrefabsCount, 1f / 3f);
            int gridSize = Mathf.RoundToInt(cubeRoot);

            // Calculate the starting position to center the grid
            float startX = -(gridSize - 1) * spawnPrefabSpacing * 0.5f;
            float startY = -(gridSize - 1) * spawnPrefabSpacing * 0.5f;
            float startZ = -(gridSize - 1) * spawnPrefabSpacing * 0.5f;

            //Debug.Log($"Start Positions: X={startX}, Y={startY}, Z={startZ}, gridSize={gridSize}");

            for (int x = 0; x < gridSize; ++x)
                for (int y = 0; y < gridSize; ++y)
                    for (int z = 0; z < gridSize; ++z)
                        if (spawned < spawnPrefabsCount)
                        {
                            float x1 = startX + x * spawnPrefabSpacing;
                            float y1 = startY + y * spawnPrefabSpacing;
                            float z1 = startZ + z * spawnPrefabSpacing;
                            Vector3 position = new Vector3(x1, y1, z1);

                            NetworkServer.Spawn(Instantiate(spawnPrefab, position, Quaternion.identity, SpawnsTransform));
                            ++spawned;
                        }
        }
    }
}
