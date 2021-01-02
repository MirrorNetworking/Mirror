using UnityEngine;

namespace Mirror.TransformSyncing.Example
{
    public class ExampleNetworkManager : NetworkManager
    {
        [Header("Moving objects")]
        [SerializeField] int cubeCount = 10;
        [SerializeField] GameObject cubePrefab;

        public override void OnStartClient()
        {
            ClientScene.RegisterPrefab(cubePrefab);
        }

        public override void OnStartServer()
        {
            for (int i = 0; i < cubeCount; i++)
            {
                GameObject clone = Instantiate(cubePrefab);
                NetworkServer.Spawn(clone);
            }
        }
    }
}
