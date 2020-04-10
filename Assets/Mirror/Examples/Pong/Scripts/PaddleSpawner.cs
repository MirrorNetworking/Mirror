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
            Transform start = server.NumPlayers == 0 ? leftRacketSpawn : rightRacketSpawn;
            NetworkIdentity player = Instantiate(playerPrefab, start.position, start.rotation);
            server.AddPlayerForConnection(conn, player.gameObject);

            // spawn ball if two players
            if (server.NumPlayers == 2)
            {
                ball = Instantiate(ballPrefab);
                server.Spawn(ball);
            }
        }


        public void OnServerDisconnect(INetworkConnection conn)
        {
            // destroy ball
            if (ball != null)
                server.Destroy(ball);
        }
    }
}