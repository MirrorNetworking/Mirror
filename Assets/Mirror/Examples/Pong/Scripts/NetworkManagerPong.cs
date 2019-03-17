// custom NetworkManager that simply assigns the correct racket positions when
// spawning players. the built in RoundRobin spawn method wouldn't work after
// someone reconnects (both players would be on the same side).
using UnityEngine;
using Mirror;

public class NetworkManagerPong : NetworkManager
{
    public Transform leftRacketSpawn;
    public Transform rightRacketSpawn;

    public override void OnServerAddPlayer(NetworkConnection conn, AddPlayerMessage extraMessage)
    {
        Transform start = numPlayers == 0 ? leftRacketSpawn : rightRacketSpawn;
        GameObject player = Instantiate(playerPrefab, start.position, start.rotation);
        NetworkServer.AddPlayerForConnection(conn, player);
    }
}
