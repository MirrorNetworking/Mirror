# NetworkServer

NetworkServer is a High-Level-API class that manages connections from multiple clients.

## Properties
-   **active**  
    Checks if the server has been started.
-   **connections**  
    A list of all the current connections from clients.
-   **dontListen**  
    If you enable this, the server will not listen for incoming connections on the regular network port.
-   **handlers**  
    Dictionary of the message handlers registered with the server.
-   **hostTopology**  
    The host topology that the server is using.
-   **localClientActive**  
    True if a local client is currently active on the server.
-   **localConnection**  
    The connection to the local client. This is only used when in host mode

## Methods
-   static bool **AddConnection**(NetworkConnection conn)
-   static bool **AddPlayerForConnection**(NetworkConnection conn, GameObject player)
-   static bool **AddPlayerForConnection**(NetworkConnection conn, GameObject player, Guid assetId)
-   static void **ClearHandlers**()
-   static void **Destroy**(GameObject obj)
-   static void **DestroyPlayerForConnection**(NetworkConnection conn)
-   static void **DisconnectAll**()
-   static void **DisconnectAllConnections**()
-   static bool **Listen**(int maxConns)
-   static void **RegisterHandler**\<T\>(Action\<NetworkConnection, T\> handler)
-   static bool **RemoveConnection**(int connectionId)
-   static bool **ReplacePlayerForConnection**(NetworkConnection conn, GameObject player)
-   static bool **ReplacePlayerForConnection**(NetworkConnection conn, GameObject player, Guid assetId)
-   static void **Reset**()
-   static bool **SendToAll**\<T\>(T msg, int channelId = Channels.DefaultReliable)
-   static void **SendToClient**\<T\>(int connectionId, T msg)
-   static void **SendToClientOfPlayer**\<T\>(NetworkIdentity identity, T msg)
-   static void **SetAllClientsNotReady**()
-   static void **SetClientNotReady**(NetworkConnection conn)
-   static void **SetClientReady**(NetworkConnection conn)
-   static void **Shutdown**()
-   static void **Spawn**(GameObject obj)
-   static void **Spawn**(GameObject obj, Guid assetId)
-   static bool **SpawnObjects**()
-   static bool **SpawnWithClientAuthority**(GameObject obj, GameObject player)
-   static bool **SpawnWithClientAuthority**(GameObject obj, NetworkConnection conn)
-   static bool **SpawnWithClientAuthority**(GameObject obj, Guid assetId, NetworkConnection conn)
-   static void **UnregisterHandler**\<T\>()
-   static void **UnSpawn**(GameObject obj)
