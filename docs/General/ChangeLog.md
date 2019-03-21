# Change Log

## Version 1.7 -- In Progress

- Added: Semantic Versioning
- Added: SyncDictionary...SyncHashSet coming soon™
- Added: NoRotation to NetworkTransform
- Added: Scale is now included in spawn payload along with position and rotation
- Added: Generic `IMessageBase` to allow struct message types
- Fixed: Host should not call Disconnect on transports
- Fixed: NetworkLobbyPlayer.OnClientReady works now
- Fixed: NetworkLobbyManager `pendingPlayers` and `lobbySlots` lists are now public for inheritors
- Fixed: Offline scene switching now works via `StopClient()`
- Fixed: Pong example updated
- Changed: TargetRpc NetworkConnection paramater is now optional...the calling clients' NetworkConnection is default
- Changed: Movement example replaced with Tank example
- Changed: SyncList now supports structs directly, making SyncListSTRUCT obsolete.
- Removed: SyncListSTRUCT - Use SyncList instead.

## Version 1.6 -- 2019-Mar-14

- Fixed: Websockets transport moved to Mirror.Websocket namespace
- Fixed: NetworkAnimator bandwidth abuse
- Fixed: NetworkAnimator float sync bug
- Fixed: Persistent SceneID's for Networked objects
- Changed: Documentation for [Transports](../Transports)
- Changed: Weaver is now full source...FINALLY!
- Changed: ClientScene.AddPlayer 2nd parameter is now `byte[] extraData` instead of `MessageBase extraMessage` 
- Changed: NetworkManager -- Headless Auto-Start moved to `Start()` from `Awake()`
- Changed: Removed Message ID's for all messages - See [Network Messages](../Concepts/Communications/NetworkMessages) for details  
    - Message IDs are now generated automatically based on the message name.  
    - Previously you would call Send(MyMessage.MsgId, message), now you call Send(message)
- Removed: Documentation for Groove Transport - use Websockets Transport instead

## Version 1.5 -- 2019-Mar-01

- Added: **Migration Tool** to (mostly) automate conversion from UNet
- Added: Full support for WebSockets and WebSocketsSecure to replace UNet LLAPI
- Added: Transport Multiplexer - allows the use of multiple concurrent transports
- Added: NetworkLobbyManager and NetworkLobbyPlayer with example game
- Added: Configurable Server Tickrate in NetworkManager
- Added: New virtual OnClientChangeScene fires right before SceneManager.LoadSceneAsync is executed
- Added: Unit tests for Weaver
- Fixed: Garbage allocations removed from a lot of things (more work to do, we know)
- Fixed: NetworkProximityChecker now uses OverlapSphereNonAlloc and OverlapCircleNonAlloc
- Fixed: SyncVar hook not firing when clients joined
- Fixed: NetworkManager no longer assumes it's on Scene(0) in Build Settings
- Fixed: NetworkAnimator no longer lmited to 6 variables
- Fixed: TelepathyTransport delivering messages when disabled
- Changed: Minimum Unity version: **2018.3.6**
- Removed: SceneAttribute.cs (merged to CustomAttributes.cs)
- Removed: NetworkClient.allClients (Use NetworkClient.singleton instead)
- Removed: NetworkServer.hostId and NetworkConnection.hostId (holdovers from LLAPI)
- Removed: NetworkConnection.isConnected (NetworkConnection is always connected)
- Removed: Transport.GetConnectionInfo (Use ServerGetClientAddress instead)


## Version 1.4 -- 2019-Feb-01

- Added: HelpURL attirbutes to components
- Added: Automatic targetFramerate for headless builds
- Added: ByteMessage to Messages class
- Fixed: Connectiing state can be cancelled properly
- Fixed: NetworkTransformBase interpolation applied to client's own object
- Fixed: Objects are spawned with correct rotation
- Fixed: SceneId assignment
- Fixed: Changed syncInterval wasn't saved...it is now
- Fixed: Additive Scene loading
- Changed: **Mirror is now full source** -- no more DLL's
- Changed: **Transports are now components** -- TCP, UDP, WebGL, Steam
- Changed: Transport class now dispatches Unity Events
- Changed: NetworkServer.SendToClientOfPlayer uses NetworkIdentity now
- Changed: NetworkServer.SendToObservers uses NetworkIdentity parameter now
- Changed: NetworkServer.SendToReady uses NetworkIdentity now
- Changed: NetworkServer.DestroyPlayerForConnection uses NetworkIdentity.spawned now
- Changed: NetworkConnection.Dispose uses NetworkIdentity.spawned now
- Changed: NetworkReader.ReadTransform uses NetworkIdentity.spawned now
- Changed: NetworkTransform reimplemented -- physics removed, code simplified
- Removed: NetworkClient.hostPort (port is handled at Transport level)
- Removed: NetworkServer.FindLocalObject (Use NetworkIdentity.spawned\[netId\] instead)
- Removed: ClientScene.FindLocalObject (Use NetworkIdentity.spawned\[netId\] instead)
