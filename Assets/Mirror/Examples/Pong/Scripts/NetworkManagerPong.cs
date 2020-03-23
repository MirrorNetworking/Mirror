using UnityEngine;

namespace Mirror.Examples.Pong
{
    // Custom NetworkManager that simply assigns the correct racket positions when
    // spawning players. The built in RoundRobin spawn method wouldn't work after
    // someone reconnects (both players would be on the same side).
    [AddComponentMenu("")]
    public class NetworkManagerPong : NetworkManager
    {
        public Transform leftRacketSpawn;
        public Transform rightRacketSpawn;
        GameObject ball;

        public override void OnServerAddPlayer(NetworkConnection conn)
        {
            // add player at correct spawn position
            Transform start = numPlayers == 0 ? leftRacketSpawn : rightRacketSpawn;
            GameObject player = Instantiate(server.playerPrefab, start.position, start.rotation);
            server.AddPlayerForConnection(conn, player);

            // spawn ball if two players
            if (numPlayers == 2)
            {
                ball = Instantiate(client.spawnPrefabs.Find(prefab => prefab.name == "Ball"));
                server.Spawn(ball);
            }
        }

        public override void OnServerDisconnect(NetworkConnection conn)
        {
            // destroy ball
            if (ball != null)
                server.Destroy(ball);

            // call base functionality (actually destroys the player)
            base.OnServerDisconnect(conn);
        }
    }
}
