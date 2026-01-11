using System;
using UnityEngine;

namespace Mirror.Examples.Hex2D
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(HexSpatialHash2DInterestManagement))]
    public class Hex2DNetworkManager : NetworkManager
    {
        // Overrides the base singleton so we don’t have to cast to this type everywhere.
        public static new Hex2DNetworkManager singleton => (Hex2DNetworkManager)NetworkManager.singleton;

        [Header("Spawns")]
        public GameObject spawnPrefab;

        [Range(1, 3000), Tooltip("Number of prefabs to spawn in a flat 2D grid across the scene.")]
        public ushort spawnPrefabsCount = 1000;

        [Range(1, 10), Tooltip("Spacing between grid points in meters.")]
        public byte spawnPrefabSpacing = 3;

        [Header("Diagnostics")]
        [ReadOnly, SerializeField] HexSpatialHash2DInterestManagement hexSpatialHash2DInterestManagement;

        public override void OnValidate()
        {
            if (Application.isPlaying) return;
            base.OnValidate();

            if (hexSpatialHash2DInterestManagement == null)
                hexSpatialHash2DInterestManagement = GetComponent<HexSpatialHash2DInterestManagement>();
        }

        public override void OnStartClient()
        {
            NetworkClient.RegisterPrefab(spawnPrefab);
        }

        public override void OnStartServer()
        {
            // Instantiate an empty GameObject to parent spawns
            GameObject spawns = new GameObject("Spawns");
            Transform spawnsTransform = spawns.transform;

            int spawned = 0;

            // Spawn prefabs in a 2D grid centered around origin (0,0,0)
            int gridSize = (int)Mathf.Sqrt(spawnPrefabsCount); // Square grid size based on count

            // Calculate the starting position to center the grid at (0,0,0)
            float halfGrid = (gridSize - 1) * spawnPrefabSpacing * 0.5f;
            float startX = -halfGrid;
            float startZorY = -halfGrid; // Z for XZ, Y for XY

            //Debug.Log($"Start Positions: X={startX}, Z/Y={startZorY}, gridSize={gridSize}");

            // Use a 2D loop for a flat grid
            for (int x = 0; x < gridSize && spawned < spawnPrefabsCount; ++x)
            {
                for (int zOrY = 0; zOrY < gridSize && spawned < spawnPrefabsCount; ++zOrY)
                {
                    Vector3 position = Vector3.zero;

                    if (hexSpatialHash2DInterestManagement.checkMethod == HexSpatialHash2DInterestManagement.CheckMethod.XZ_FOR_3D)
                    {
                        float xPos = startX + x * spawnPrefabSpacing;
                        float zPos = startZorY + zOrY * spawnPrefabSpacing;
                        position = new Vector3(xPos, 0.5f, zPos);
                    }
                    else // XY_FOR_2D
                    {
                        float xPos = startX + x * spawnPrefabSpacing;
                        float yPos = startZorY + zOrY * spawnPrefabSpacing;
                        position = new Vector3(xPos, yPos, -0.5f);
                    }

                    GameObject instance = Instantiate(spawnPrefab, position, Quaternion.identity, spawnsTransform);
                    NetworkServer.Spawn(instance);
                    ++spawned;
                }
            }

            //Debug.Log($"Spawned {spawned} objects in a {gridSize}x{gridSize} 2D grid.");
        }
    }
}
