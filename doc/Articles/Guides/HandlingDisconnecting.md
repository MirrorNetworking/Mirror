# Handling client disconnects

A networked application should implement disconnect-functionality - i.e. take the client to an offline scene, for example, and allow for the opportunity to reconnect.

_In case of a) the client itself terminating a connection or experiencing a disconnection, or b) the server dropping the client, the networked objects of which the client has authority will be destroyed by the server. This is the intended design; keep in mind [Authority](Authority.md) is a way of deciding who owns an object and has control over it; so these objects must have their ownership transferred to the server and/or other clients, if they are to 'survive' when their owner is disconnected._


## Acting on disconnects

The NetworkManager has a [OnClientDisconnect](https://mirror-networking.com/docs/api/Mirror.NetworkManager.html#Mirror_NetworkManager_OnClientDisconnect_Mirror_NetworkConnection_) method that can be overridden to  allow for custom functionality on the client when that client disconnects. For example:

```cs
public class CustomNetworkManager : NetworkManager
{
    public override void OnClientDisconnect(NetworkConnection conn)
    {
        /*
         *  Execute custom functionality to react, client-side, to disconnects here. For example, send the client to an offline-scene.
         */

        base.OnClientDisconnect(conn);
    }
}
```

On the server, the  [OnServerDisconnect](https://mirror-networking.com/docs/api/Mirror.NetworkManager.html#Mirror_NetworkManager_OnServerDisconnect_Mirror_NetworkConnection_) method can be overridden to allow for custom functionality when any client disconnects. For example:

```cs
public class CustomNetworkManager : NetworkManager
{
    public override void OnServerDisconnect(NetworkConnection conn)
    {
        /*
         *  Execute custom functionality to react, server-side, to disconnects here.
         */

        base.OnServerDisconnect(conn);
    }
}
```

## Default disconnection strategy

The NetworkManager has settings to control if inactive clients should be automatically disconnected:

![NetworkManager automatic disconnection settings](NetworkManager_autoDiscnt_settings.jpg)


## Deliberately disconnecting the client

The preferred way for a client to deliberately disconnect itself is to call the [StopClient()](https://mirror-networking.com/docs/api/Mirror.NetworkManager.html#Mirror_NetworkManager_StopClient) function on the NetworkManager.

For the purpose of disconnecting all clients, the NetworkServer-class has two suitable methods; [DisconnectAll()](https://mirror-networking.com/docs/api/Mirror.NetworkServer.html#Mirror_NetworkServer_DisconnectAll) and [DisconnectAllConnections()](https://mirror-networking.com/docs/api/Mirror.NetworkServer.html#Mirror_NetworkServer_DisconnectAllConnections). The first disconnects all clients. The other disconnects all clients who are not also host. Use DisconnectAllConnections() if in a host+client scenario.


## Disconnects at the transport level

Mirror's high-level API abstracts away the low-level specificities of the selected transport. These transports may offer their own disconnect-methods, or disconnect-events, that can be handled to further deal with disconnects on a lower level - if, though unlikely, Mirror's high-level approach do not cover a specific scenario.

