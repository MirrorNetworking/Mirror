# Network Messages

For the most part we recommend the high level [Commands and RPC](RemoteActions.md) calls and [SyncVar](../StateSync.md), but you can also send low level network messages. This can be useful if you want clients to send messages that are not tied to game objects, such as logging, analytics or profiling information.

There is a class called MessageBase that you can extend to make serializable network message classes. This class has Serialize and Deserialize functions that take writer and reader objects. You can implement these functions yourself, but we recommend you let Mirror generate them for you.

The base class looks like this:

``` cs
public abstract class MessageBase
{
    // Deserialize the contents of the reader into this message
    public virtual void Deserialize(NetworkReader reader) {}

    // Serialize the contents of this message into the writer
    public virtual void Serialize(NetworkWriter writer) {}
}
```

The auto generated Serialize/Deserialize can efficiently deal with basic types, structs, arrays and common Unity value types such as Color, Vector3, Quaternion. Make your members public. If you need class members or complex containers such as List and Dictionary, you must implement the Serialize and Deserialize methods yourself.

To send a message, use the `Send()` method on the NetworkClient, NetworkServer, and NetworkConnection classes which work the same way. It takes a message object that is derived from MessageBase. The code below demonstrates how to send and handle a message:

To declare a custom network message class and use it:

``` cs
using UnityEngine;
using Mirror;

public class Scores : MonoBehaviour
{
    public class ScoreMessage : MessageBase
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

    public void SetupClient()
    {
        NetworkClient.RegisterHandler<ScoreMessage>(OnScore);
        NetworkClient.Connect("localhost");
    }

    public void OnScore(NetworkConnection conn, ScoreMessage msg)
    {
        Debug.Log("OnScoreMessage " + msg.score);
    }
}
```

Note that there is no serialization code for the `ScoreMessage` class in this source code example. The body of the serialization functions is automatically generated for this class by Mirror.
