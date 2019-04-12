// custom NetworkManager that simply assigns the correct racket positions when
// spawning players. the built in RoundRobin spawn method wouldn't work after
// someone reconnects (both players would be on the same side).
using UnityEngine;
using Mirror;

public class NetworkManagerPong : NetworkManager
{
    public Transform leftRacketSpawn;
    public Transform rightRacketSpawn;
    public GameObject ballPrefab;
    GameObject ball;

    public override void OnServerAddPlayer(NetworkConnection conn, AddPlayerMessage extraMessage)
    {
        // add player at correct spawn position
        Transform start = numPlayers == 0 ? leftRacketSpawn : rightRacketSpawn;
        GameObject player = Instantiate(playerPrefab, start.position, start.rotation);
        NetworkServer.AddPlayerForConnection(conn, player);

        // spawn ball if two players
        if (numPlayers == 2)
        {
            ball = Instantiate(ballPrefab);
            NetworkServer.Spawn(ball);
        }
    }

    public override void OnServerDisconnect(NetworkConnection conn)
    {
        // destroy ball
        if (ball != null)
            NetworkServer.Destroy(ball);

        // call base functionality (actually destroys the player)
        base.OnServerDisconnect(conn);
    }
}
