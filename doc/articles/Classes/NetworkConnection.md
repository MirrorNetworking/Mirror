# NetworkConnection

Network Connection is a high-level API class that encapsulates a network connection. `NetworkClient` objects have a single connection, and `NetworkServer`'s have multiple connections - one from each client. Network Connections have the ability to send byte arrays or serialized objects as network messages.

## Constructors
-   **NetworkConnection**()
-   **NetworkConnection**(string networkAddress)
-   **NetworkConnection**(string networkAddress, int networkConnectionId)

## Properties
-   **address**  
    The IP address of the end-point that this connection is connected to.
-   **clientOwnedObjects**  
    The `HashSet` of objects that this connection has authority over.
-   **connectionId**  
    The incremented connectionId for this connection.
-   **hostId**  
    Deprecated.  Use `connection.GetType() == typeof(NetworkConnection.md)` to check if it's a regular or local connection.
-   **isReady**  
    Flag to control whether state updates are sent to this connection
-   **lastMessageTime**  
    The last time that a message was received on this connection.
-   **playerController**  
    A reference to the [NetworkIdentity](../Components/NetworkIdentity.md) playerController.

## Methods
-   void **Disconnect**()
-   bool **InvokeHandler**\<T\>(T msg)
-   virtual bool **Send**\<T\>(T msg, int channelId = Channels.DefaultReliable)
-   virtual void **TransportReceive**(ArraySegment\<byte\> buffer)
-   virtual bool **TransportSend**(int channelId, byte[] bytes)

The NetworkConnection class has virtual functions that are called when data is sent to the transport layer or received from the transport layer. These functions allow specialized versions of NetworkConnection to inspect or modify this data, or even route it to different sources. These function are shown below, including the default behaviour:

```cs
public virtual void TransportRecieve(byte[] bytes, int numBytes, int channelId)
{
    HandleBytes(bytes, numBytes, channelId);
}

public virtual bool TransportSend(byte[] bytes, int numBytes, int channelId, out byte error)
{
    return NetworkTransport.Send(hostId, connectionId, channelId, bytes, numBytes, out error);
}
```

An example use of these function is to log the contents of incoming and outgoing packets. Below is an example of a `DebugConnection` class that is derived from `NetworkConnection` that logs the first 50 bytes of packets to the console. To use a class like this call the `SetNetworkConnectionClass()` function on a `NetworkClient` or `NetworkServer`.

```cs
class DebugConnection : NetworkConnection
{
    public override void TransportRecieve(byte[] bytes, int numBytes, int channelId)
    {
        StringBuilder msg = new StringBuilder();

        for (int i = 0; i < numBytes; i++)
        {
            var s = String.Format("{0:X2}", bytes[i]);
            msg.Append(s);
            if (i > 50) break;
        }

        UnityEngine.Debug.Log("TransportRecieve h:" + hostId + " con:" + connectionId + " bytes:" + numBytes + " " + msg);

        HandleBytes(bytes, numBytes, channelId);
    }

    public override bool TransportSend(byte[] bytes, int numBytes, int channelId, out byte error)
    {
        StringBuilder msg = new StringBuilder();

        for (int i = 0; i < numBytes; i++)
        {
            var s = String.Format("{0:X2}", bytes[i]);
            msg.Append(s);
            if (i > 50) break;
        }

        UnityEngine.Debug.Log("TransportSend    h:" + hostId + " con:" + connectionId + " bytes:" + numBytes + " " + msg);

        return NetworkTransport.Send(hostId, connectionId, channelId, bytes, numBytes, out error);
    }
}
```
