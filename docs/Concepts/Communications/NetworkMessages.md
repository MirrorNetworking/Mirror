# Network Messages

In addition to “high-level” Commands and RPC calls, you can also send raw network messages.

There is a class called MessageBase that you can extend to make serializable network message classes. This class has Serialize and Deserialize functions that take writer and reader objects. You can implement these functions yourself, or you can rely on code-generated implementations that are automatically created by the networking  
system. The base class looks like this:

```
public abstract class MessageBase
{
    // De-serialize the contents of the reader into this message
    public virtual void Deserialize(NetworkReader reader) {}

    // Serialize the contents of this message into the writer
    public virtual void Serialize(NetworkWriter writer) {}
}
```

Message classes can contain members that are basic types, structs, and arrays, including most of the common Unity Engine types (such as Vector3). They cannot contain members that are complex classes or generic containers. Remember that if you want to rely on the code-generated implementations, you must make sure your types are publicly visible.

There are built-in message classes for common types of network messages:

-   EmptyMessage
-   StringMessage
-   IntegerMessage

To send a message, use the `Send()` method on the NetworkClient, NetworkServer, and NetworkConnection classes which work the same way. It takes a message ID, and a message object that is derived from MessageBase. The code below demonstrates how to send and handle a message using one of the built-in message classes:

```
using UnityEngine;
using Mirror;

public class Begin : NetworkBehaviour
{
    const short MyBeginMsg = 1002;

    NetworkClient m_client;

    public void SendReadyToBeginMessage(int myId)
    {
        var msg = new IntegerMessage(myId);
        m_client.Send(MyBeginMsg, msg);
    }

    public void Init(NetworkClient client)
    {
        m_client = client;
        NetworkServer.RegisterHandler(MyBeginMsg, OnServerReadyToBeginMessage);
    }

    void OnServerReadyToBeginMessage(NetworkMessage netMsg)
    {
        var beginMessage = netMsg.ReadMessage<IntegerMessage>();
        Debug.Log("received OnServerReadyToBeginMessage " + beginMessage.value);
    }
}
```

To declare a custom network message class and use it:

```
using UnityEngine;
using Mirror;

public class Scores : MonoBehaviour
{
    NetworkClient myClient;

    public class MyMsgType
    {
        public static short Score = MsgType.Highest + 1;
    };

    public class ScoreMessage : MessageBase
    {
        public int score;
        public Vector3 scorePos;
        public int lives;
    }

    public void SendScore(int score, Vector3 scorePos, int lives)
    {
        ScoreMessage msg = new ScoreMessage();
        msg.score = score;
        msg.scorePos = scorePos;
        msg.lives = lives;

        NetworkServer.SendToAll(MyMsgType.Score, msg);
    }

    // Create a client and connect to the server port
    public void SetupClient()
    {
        myClient = new NetworkClient();
        myClient.RegisterHandler(MsgType.Connect, OnConnected);
        myClient.RegisterHandler(MyMsgType.Score, OnScore);
        myClient.Connect("127.0.0.1", 4444);
    }

    public void OnScore(NetworkMessage netMsg)
    {
        ScoreMessage msg = netMsg.ReadMessage<ScoreMessage>();
        Debug.Log("OnScoreMessage " + msg.score);
    }

    public void OnConnected(NetworkMessage netMsg)
    {
        Debug.Log("Connected to server");
    }
}
```

Note that there is no serialization code for the `ScoreMessage` class in this source code example. The body of the serialization functions is automatically generated for this class by Mirror.

## ErrorMessage Class

There is also an ErrorMessage class that is derived from `MessageBase`. This class is passed to error callbacks on clients and servers.

The errorCode in the ErrorMessage class corresponds to the Networking.NetworkError enumeration.

```
class MyClient
{
    NetworkClient client;
    
    void Start()
    {
        client = new NetworkClient();
        client.RegisterHandler(MsgType.Error, OnError);
    }
    
    void OnError(NetworkMessage netMsg)
    {
        var errorMsg = netMsg.ReadMessage<ErrorMessage>();
        Debug.Log("Error:" + errorMsg.errorCode);
    }
}
```
