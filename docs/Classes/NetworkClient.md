# NetworkClient

`NetworkClient` is a [high-level
API](https://docs.unity3d.com/Manual/UNetUsingHLAPI.html) class that manages a
network connection from a client to a server, and can send and receive messages
between the client and the server. The `NetworkClient` class also helps to
manage spawned network GameObjects, and routing of
[RPC](https://docs.unity3d.com/Manual/UNetActions.html) message and network
events.

See the
[NetworkClient](https://docs.unity3d.com/ScriptReference/Networking.NetworkClient.html)
script reference for more information.

## Properties

**Property:**

**Function:**

**serverIP**

The IP address of the server that this client is connected to.

**serverPort**

The port of the server that this client is connected to.

**connection**

The NetworkConnection GameObject this `NetworkClient` instance is using.

**handlers**

The set of registered message handler functions.

**numChannels**

The number of configured NetworkTransport QoS channels.

**isConnected**

True if the client is connected to a server.

**allClients**

List of active NetworkClients (static).

**active**

True if any NetworkClients are active (static).
