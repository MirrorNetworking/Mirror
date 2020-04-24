# Deprecations

Certain features of Unity Networking (UNet) were removed from Mirror or modified for various reasons. This page will identify all changed and removed features, properties, and methods, the reason for change or removal, and possible alternatives.

>   Note: Some changes in this document may apply to an upcoming release to the Asset Store.

## Match Namespace & Host Migration

As part of the Unity Services, this entire namespace was removed. It didn't work well to begin with, and was incredibly complex to be part of the core networking package. We expect this, along with other back-end services, will be provided through standalone apps that have integration to Mirror.

## Network Server Simple

This was too complex and impractical to maintain for what little it did, and was removed. There are much easier ways to make a basic listen server, with or without one of our transports.

## Couch Co-Op

The core networking was greatly simplified by removing this low-hanging fruit. It was buggy, and too convoluted to be worth fixing.  For those that need something like this, consider defining a non-visible player prefab as a message conduit that spawns actual player prefabs with client authority.  All inputs would route through the conduit prefab to control the player objects.

## Message Types

The `MsgType` enumeration was removed. All message types are generated dynamically. Use `Send<T>` instead.

## Network Transform

[Network Transform](../Components/NetworkTransform.md) was significantly simplified so that it only syncs position, rotation and scale. Rigidbody support was removed. We may create a new NetworkRigidbody component that will be server authoritative with physics simulation and interpolation.

## Network Animator

[Network Animator](../Components/NetworkAnimator.md) was also simplified, as it batches all Animator parameters into a single update message.

## SyncVar Hook Parameters

[SyncVar](../Guides/Sync/SyncVars.md) property values are now updated before the hook is called, and hooks now require two parameters of the same type as the property: `oldValue` and `newValue`

## SyncListSTRUCT

Use `SyncList<YourSpecialStruct>` instead.

## SyncList Operations

-   `OP_REMOVE` was replaced by `OP_REMOVEAT`
-   `OP_DIRTY` was replaced by `OP_SET`

## SyncIDictionary Operations

-   `OP_DIRTY` was replaced by `OP_SET`

## Quality of Service Flags

In classic UNet, QoS Flags were used to determine how packets got to the remote end. For example, if you needed a packet to be prioritized in the queue, you would specify a high priority flag which the Unity LLAPI would then receive and deal with appropriately. Unfortunately, this caused a lot of extra work for the transport layer and some of the QoS flags did not work as intended due to buggy code that relied on too much magic.

In Mirror, QoS flags were replaced with a "Channels" system. This system paves the way for future Mirror improvements, so you can send data on different channels - for example, you could have all game activity on channel 0, while in-game text chat is sent on channel 1 and voice chat is sent on channel 2. In the future, it may be possible to assign a transport system per channel, allowing one to have a TCP transport for critical game network data on channel 0, while in-game text and voice chat is running on a UDP transport in parallel on channel 1. Some transports, such as [Ignorance](../Transports/Ignorance.md), also provide legacy compatibility for those attached to QoS flags.

The currently defined channels are:

-   `Channels.DefaultReliable = 0`

-   `Channels.DefaultUnreliable = 1`

Currently, Mirror using it's default TCP transport will always send everything over a reliable channel. There is no way to bypass this behaviour without using a third-party transport, since TCP is always reliable. Other transports may support other channel sending methods.

## Changes by Class

### NetworkManager

-   `networkPort`  
    Removed as part of separating Transports to components. Not all transports use ports, but those that do have a field for it. See [Transports](../Transports/index.md) for more info.

-   `IsHeadless()`  
    Use `isHeadless` instead, as it's a property now.

-   `client`  
    Use NetworkClient directly, it will be made static soon. For example, use `NetworkClient.Send(message)` instead of `NetworkManager.client.Send(message)`.

-   `IsClientConnected()`  
    Use static property `NetworkClient.isConnected` instead.

-   `onlineScene` and `offlineScene`  
    These store full paths now, so use SceneManager.GetActiveScene().path instead.

-   `OnStartClient(NetworkClient client)`  
    Override OnStartClient() instead since all `NetworkClient` methods are static now.

-   `OnClientChangeScene(string newSceneName)`  
    Override `OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling)` instead.

-   `OnClientChangeScene(string newSceneName, SceneOperation sceneOperation)`  
    Override `OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling)` instead.

-   `OnServerAddPlayer(NetworkConnection conn, AddPlayerMessage extraMessage)`  
    Override `OnServerAddPlayer(NetworkConnection conn)` instead. See [Custom Player Spawn Guide](../Guides/GameObjects/SpawnPlayerCustom.md) for details.

### NetworkRoomManager

-   `OnRoomServerCreateGamePlayer(NetworkConnection conn)`  
    Use `OnRoomServerCreateGamePlayer(NetworkConnection conn, GameObject roomPlayer)` instead.

-   `OnRoomServerSceneLoadedForPlayer(GameObject roomPlayer, GameObject gamePlayer)`  
    Use `OnRoomServerSceneLoadedForPlayer(NetworkConnection conn, GameObject roomPlayer, GameObject gamePlayer)` instead.

### NetworkIdentity

-   `clientAuthorityOwner`  
    Use connectionToClient instead

-   `GetSceneIdenity`  
    Use `GetSceneIdentity` instead (typo in original name)

-   `RemoveClientAuthority(NetworkConnection conn)`  
    NetworkConnection parameter is no longer needed and nothing is returned

-   Local Player Authority checkbox  
    This checkbox is no longer needed, and we simplified how [Authority](../Guides/Authority.md) works in Mirror.

### NetworkBehaviour

-   `sendInterval` attribute  
    Use `NetworkBehaviour.syncInterval` field instead. Can be modified in the Inspector too.

-   `List<SyncObject> m_SyncObjects`  
    Use `List<SyncObject> syncObjects` instead.

-   `OnSetLocalVisibility(bool visible)`  
    Override `OnSetHostVisibility(bool visible)` instead.

-   In Mirror 12, `OnRebuildObservers`, `OnCheckObserver`, and `OnSetHostVisibility` were moved to a separate class called `NetworkVisibility`

-   In Mirror 12, `NetworkBehaviour.OnNetworkDestroy` was renamed to `NetworkBehaviour.OnStopClient`.

### NetworkConnection

-   `hostId`  
    Removed because it's not needed ever since we removed LLAPI as default. It's always 0 for regular connections and -1 for local connections. Use `connection.GetType() == typeof(NetworkConnection)` to check if it's a regular or local connection.

-   `isConnected`  
    Removed because it's pointless. A NetworkConnection is always connected.

-   `InvokeHandlerNoData(int msgType)`  
    Use `InvokeHandler<T>` instead.

-   `playerController`  
    renamed to `identity` since that's what it is: the `NetworkIdentity` for the connection. If you need to convert a project after this change, Visual Studio / VS Code can help...read more [here](PlayerControllerToIdentity.md).

-   `RegisterHandler(short msgType, NetworkMessageDelegate handler)`  
    Use `NetworkServer.RegisterHandler<T>()` or `NetworkClient.RegisterHandler<T>()` instead.

-   `UnregisterHandler(short msgType)`  
    Use `NetworkServer.UnregisterHandler<T>()` or `NetworkClient.UnregisterHandler<T>()` instead.

-   `Send(int msgType, MessageBase msg, int channelId = Channels.DefaultReliable)`  
    Use `Send<T>(msg, channelId)` instead.

### NetworkServer

-   `FindLocalObject(uint netId)`  
    Use `NetworkIdentity.spawned[netId].gameObject` instead.

-   `RegisterHandler(int msgType, NetworkMessageDelegate handler)`  
    Use `RegisterHandler<T>(T msg)` instead.

-   `RegisterHandler(MsgType msgType, NetworkMessageDelegate handler)`  
    Use `RegisterHandler<T>(T msg)` instead.

-   `UnregisterHandler(int msgType)`  
    Use `UnregisterHandler<T>(T msg)` instead.

-   `UnregisterHandler(MsgType msgType)`  
    Use `UnregisterHandler<T>(T msg)` instead.

-   `SendToAll(int msgType, MessageBase msg, int channelId = Channels.DefaultReliable)`  
    Use `SendToAll<T>(T msg, int channelId = Channels.DefaultReliable)` instead.

-   `SendToClient(int connectionId, int msgType, MessageBase msg)`  
    Use `NetworkConnection.Send<T>(T msg, int channelId = Channels.DefaultReliable)` instead.

-   `SendToClient<T>(int connectionId, T msg)`  
    Use `NetworkConnection.Send<T>(T msg, int channelId = Channels.DefaultReliable)` instead.

-   `SendToClientOfPlayer(NetworkIdentity identity, int msgType, MessageBase msg)`  
    Use `SendToClientOfPlayer<T>(NetworkIdentity identity, T msg, int channelId = Channels.DefaultReliable)` instead.

-   `SendToReady(NetworkIdentity identity, short msgType, MessageBase msg, int channelId = Channels.DefaultReliable)`  
    Use `SendToReady<T>(NetworkIdentity identity, T msg, int channelId = Channels.DefaultReliable)` instead.

-   `SpawnWithClientAuthority(GameObject obj, GameObject player)`  
    Use `Spawn(GameObject, GameObject)` instead.

-   `SpawnWithClientAuthority(GameObject obj, NetworkConnection ownerConnection)`  
    Use `Spawn(obj, connection)` instead.

-   `SpawnWithClientAuthority(GameObject obj, Guid assetId, NetworkConnection ownerConnection)`  
    Use `Spawn(obj, assetId, connection)` instead

### NetworkClient

-   `NetworkClient singleton`  
    Use `NetworkClient` directly. Singleton isn't needed anymore as all functions are static now.  
    Example: `NetworkClient.Send(message)` instead of `NetworkClient.singleton.Send(message)`.

-   `allClients`  
    Use `NetworkClient` directly instead. There is always exactly one client.

-   `GetRTT()`  
    Use `NetworkTime.rtt` instead.

-   `RegisterHandler(int msgType, NetworkMessageDelegate handler)`  
    Use `RegisterHandler<T>(T msg)` instead.

-   `RegisterHandler(MsgType msgType, NetworkMessageDelegate handler)`  
    Use `RegisterHandler<T>(T msg)` instead.

-   `UnregisterHandler(int msgType)`  
    Use `UnregisterHandler<T>(T msg)` instead.

-   `UnregisterHandler(MsgType msgType)`  
    Use `UnregisterHandler<T>(T msg)` instead.

-   `Send(short msgType, MessageBase msg)`  
    Use `Send<T>(T msg, int channelId = Channels.DefaultReliable)` with no message id instead

-   `ShutdownAll()`  
    Use `Shutdown()` instead. There is only one client.

### ClientScene

-   `FindLocalObject(uint netId)`  
    Use `NetworkIdentity.spawned[netId]` instead.

### Messages

Basic messages of simple types were all removed as unnecessary bloat. You can create your own message classes instead.

-   `StringMessage`
-   `ByteMessage`
-   `BytesMessage`
-   `IntegerMessage`
-   `DoubleMessage`
-   `EmptyMessage`

### NetworkWriter

-   `Write(bool value)`  
    Use `WriteBoolean` instead.

-   `Write(byte value)`  
    Use `WriteByte` instead.

-   `Write(sbyte value)`  
    Use `WriteSByte` instead.

-   `Write(short value)`  
    Use `WriteInt16` instead.

-   `Write(ushort value)`  
    Use `WriteUInt16` instead.

-   `Write(int value)`  
    Use `WriteInt32` instead.

-   `Write(uint value)`  
    Use `WriteUInt32` instead.

-   `Write(long value)`  
    Use `WriteInt64` instead.

-   `Write(ulong value)`  
    Use `WriteUInt64` instead.

-   `Write(float value)`  
    Use `WriteSingle` instead.

-   `Write(double value)`  
    Use `WriteDouble` instead.

-   `Write(decimal value)`  
    Use `WriteDecimal` instead.

-   `Write(string value)`  
    Use `WriteString` instead.

-   `Write(char value)`  
    Use `WriteChar` instead.

-   `Write(Vector2 value)`  
    Use `WriteVector2` instead.

-   `Write(Vector2Int value)`  
    Use `WriteVector2Int` instead.

-   `Write(Vector3 value)`  
    Use `WriteVector3` instead.

-   `Write(Vector3Int value)`  
    Use `WriteVector3Int` instead.

-   `Write(Vector4 value)`  
    Use `WriteVector4` instead.

-   `Write(Color value)`  
    Use `WriteColor` instead.

-   `Write(Color32 value)`  
    Use `WriteColor32` instead.

-   `Write(Guid value)`  
    Use `WriteGuid` instead.

-   `Write(Transform value)`  
    Use `WriteTransform` instead.

-   `Write(Quaternion value)`  
    Use `WriteQuaternion` instead.

-   `Write(Rect value)`  
    Use `WriteRect` instead.

-   `Write(Plane value)`  
    Use `WritePlane` instead.

-   `Write(Ray value)`  
    Use `WriteRay` instead.

-   `Write(Matrix4x4 value)`  
    Use `WriteMatrix4x4` instead.

-   `Write(NetworkIdentity value)`  
    Use `WriteNetworkIdentity` instead.

-   `Write(GameObject value)`  
    Use `WriteGameObject` instead.

-   `Write(byte[] buffer, int offset, int count)`  
    Use `WriteBytes` instead.

### Transport

-   `GetConnectionInfo(int connectionId, out string address)`  
    Use `ServerGetClientAddress(int connectionId)` instead.

### TelepathyTransport

-   `MaxMessageSize`  
    Use `MaxMessageSizeFromClient` or `MaxMessageSizeFromServer` instead.
