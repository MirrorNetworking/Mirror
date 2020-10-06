# Network Messages

For the most part we recommend the high level [RPC](RemoteActions.md) calls and [SyncVar](../Sync/index.md), but you can also send low level network messages. This can be useful if you want clients to send messages that are not tied to game objects, such as logging, analytics or profiling information.

You can send any [supported mirror type](../DataTypes.md) as a message, use the `Send()` method on the NetworkClient, NetworkServer, and INetworkConnection classes which work the same way. The code below demonstrates how to send and handle a message:

To declare a custom network message class and use it:

``` cs
using UnityEngine;
using Mirror;

public class Scores : MonoBehaviour
{
    // attach these in the inspector
    public NetworkServer Server;
    public NetworkClient Client;

    public class ScoreMessage
    {
        public int score;
        public Vector3 scorePos;
        public int lives;
    }

    public void SendScore(int score, Vector3 scorePos, int lives)
    {
        ScoreMessage msg = new ScoreMessage()
        {
            score = score,
            scorePos = scorePos,
            lives = lives
        };

        NetworkServer.SendToAll(msg);
    }

    public void Start() {
        Client.Connected.AddListener(OnConnected);
    }

    public void OnConnected(INetworkConnection connection)
    {
        connection.RegisterHandler<ScoreMessage>(OnScore);
    }

    public void OnScore(NetworkConnection conn, ScoreMessage msg)
    {
        Debug.Log("OnScoreMessage " + msg.score);
    }
}
```

Note that there is no serialization code for the `ScoreMessage` class in this source code example. Mirror will generate a reader and writer for ScoreMessage when it sees that it is being sent.
