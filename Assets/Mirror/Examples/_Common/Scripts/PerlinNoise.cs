using UnityEngine;
using UnityEditor;

namespace Mirror.Examples.Common.Controllers.Player
{
    [ExecuteInEditMode]
    [AddComponentMenu("")]
    public class PerlinNoise : MonoBehaviour
    {
        public float scale = 20f;
        public float heightMultiplier = .03f;
        public float offsetX = 5f;
        public float offsetY = 5f;

        [ContextMenu("Generate Terrain")]
        void GenerateTerrain()
        {
            Terrain terrain = GetComponent<Terrain>();
            if (terrain == null)
            {
                Debug.LogError("No Terrain component found on this GameObject.");
                return;
            }
#if UNITY_EDITOR
            Undo.RecordObject(terrain, "Generate Perlin Noise Terrain");
#endif
            terrain.terrainData = GenerateTerrainData(terrain.terrainData);
        }

        TerrainData GenerateTerrainData(TerrainData terrainData)
        {
            int width = terrainData.heightmapResolution;
            int height = terrainData.heightmapResolution;

            float[,] heights = new float[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float xCoord = (float)x / width * scale + offsetX;
                    float yCoord = (float)y / height * scale + offsetY;

                    heights[x, y] = Mathf.PerlinNoise(xCoord, yCoord) * heightMultiplier;
                }
            }

            terrainData.SetHeights(0, 0, heights);
            return terrainData;
        }
    }
}
