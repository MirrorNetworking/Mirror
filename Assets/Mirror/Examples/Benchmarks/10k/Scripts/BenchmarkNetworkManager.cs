using System;

namespace Mirror.Examples
{
    public class BenchmarkNetworkManager : NetworkManager
    {
        /// <summary>
        /// hook for benchmarking
        /// </summary>
        public Action BeforeLateUpdate;
        /// <summary>
        /// hook for benchmarking
        /// </summary>
        public Action AfterLateUpdate;

        NetworkSceneManager sceneManager;

        public override void Start()
        {
            sceneManager = GetComponent<NetworkSceneManager>();
        }

        public void LateUpdate()
        {
            BeforeLateUpdate?.Invoke();
            sceneManager.LateUpdate();
            AfterLateUpdate?.Invoke();
        }
    }
}
