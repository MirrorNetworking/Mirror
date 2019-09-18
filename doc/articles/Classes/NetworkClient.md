# NetworkClient

`NetworkClient` is a high-level API class that manages a network connection from a client to a server, and can send and receive messages between the client and the server. The `NetworkClient` class also helps to manage spawned network game object, and routing of RPC message and network events.

## Properties
-   **active**  
    True while a client is connecting / connected.
-   **allClients**  
    Deprecated.  Use NetworkClient directly instead. There is always exactly one client.
-   **connection**  
    The NetworkConnection game object this `NetworkClient` instance is using.
-   **handlers**  
    The set of registered message handler functions.
-   **isConnected**  
    True if the client is connected to a server.
-   **numChannels**  
    Deprecated.  QoS channels are available in some [Transports].
-   **serverIP**  
    The IP address of the server this client is connected to.
-   **serverPort**  
    Deprecated.  Port was moved to the [Transports](../Transports/index.md) that support it.

## Methods
-   static void **Connect**(string address)
-   static void **Disconnect**()
-   static void **RegisterHandler**\<T\>(Action\<NetworkConnection, T\> handler)
-   static bool **Send**\<T\>(T message, int channelId = Channels.DefaultReliable)
-   static void **Shutdown**()
-   static void **UnregisterHandler**\<T\>()

 
