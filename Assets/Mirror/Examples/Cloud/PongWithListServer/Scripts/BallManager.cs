using Mirror.Cloud.Example;
using UnityEngine;

namespace Mirror.Cloud.Examples.Pong
{
    public class BallManager : NetworkBehaviour
    {
        [SerializeField] GameObject ballPrefab = null;
        GameObject ball;
        NetworkManagerListServerPong manager;

        public override void OnStartServer()
        {
            manager = (NetworkManager.singleton as NetworkManagerListServerPong);
            manager.onPlayerListChanged += onPlayerListChanged;
        }
        public override void OnStopServer()
        {
            manager.onPlayerListChanged -= onPlayerListChanged;
        }

        private void onPlayerListChanged(int playerCount)
        {
            if (playerCount >= 2)
            {
                SpawnBall();
            }
            if (playerCount < 2)
            {
                DestroyBall();
            }
        }

        void SpawnBall()
        {
            if (ball != null)
                return;

            ball = Instantiate(ballPrefab);
            NetworkServer.Spawn(ball);
        }

        void DestroyBall()
        {
            if (ball == null)
                return;

            // destroy ball
            NetworkServer.Destroy(ball);
            ball = null;
        }
    }
}
