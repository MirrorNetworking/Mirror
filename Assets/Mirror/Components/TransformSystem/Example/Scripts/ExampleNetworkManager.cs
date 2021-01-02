using UnityEngine;

namespace Mirror.TransformSyncing.Example
{
    public class ExampleNetworkManager : NetworkManager
    {
        [Header("Moving objects")]
        [SerializeField] int cubeCount = 10;
        [SerializeField] GameObject cubePrefab;

        NetworkTransformSystem transformSystem;
        public override void Awake()
        {
            base.Awake();
            transformSystem = GetComponent<NetworkTransformSystem>();
        }

        public override void OnStartClient()
        {
            ClientScene.RegisterPrefab(cubePrefab);

            transformSystem.RegisterHandlers();
        }

        public override void OnStartServer()
        {
            for (int i = 0; i < cubeCount; i++)
            {
                GameObject clone = Instantiate(cubePrefab);
                NetworkServer.Spawn(clone);
            }

            transformSystem.RegisterHandlers();
        }

        public override void OnStopClient()
        {
            transformSystem.UnregisterHandlers();
        }
        public override void OnStopServer()
        {
            transformSystem.UnregisterHandlers();
        }
    }
}
