using UnityEngine;

namespace Mirror.Examples.Pong
{
    public class PaddleSpawner : PlayerSpawner
    {
        public Transform leftRacketSpawn;
        public Transform rightRacketSpawn;
        public GameObject ballPrefab;

        GameObject ball;

        public override void OnServerAddPlayer(INetworkConnection conn)
        {
            // add player at correct spawn position
            Transform start = Server.NumPlayers == 0 ? leftRacketSpawn : rightRacketSpawn;
            NetworkIdentity player = Instantiate(PlayerPrefab, start.position, start.rotation);
            ServerObjectManager.AddPlayerForConnection(conn, player.gameObject);

            // spawn ball if two players
            if (Server.NumPlayers == 2)
            {
                ball = Instantiate(ballPrefab);
                ServerObjectManager.Spawn(ball);
            }
        }


        public void OnServerDisconnect(INetworkConnection conn)
        {
            // destroy ball
            if (ball != null)
                ServerObjectManager.Destroy(ball);
        }
    }
}
