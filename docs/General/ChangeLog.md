# Change Log

## Version 3.x.x - In Progress

- Added: Sync-To-Owner via optional attributes and parameter for SyncVars and related classes
- Added: Support for reading and writing array segments
- Added: NetworkAnimator now has layers support
- Added: New virtual method OnServerChangeScene to NetworkManager
- Added: XML <summary> comments for intellisense and future generated class docs
- Updated Examples and Documentation
- Fixed: ScneID was not set to 0 for prefab variants
- Fixed: Observers are not properly rebuilt on scene changes
- Fixed: SyncVar hooks were not able to change other SyncVars in Host mode
- Fixed: Telepathy not setting socket options on accepted clients
- Fixed: Catch IL2CPP bug
- Fixed: Telepathy and Websockets now start connections ID's at 1 instead of 2
- Fixed: Websockets support for SSL restored
- Fixed: NetworkManager no longer complains about missing player prefab if auto-create is disabled.
- Changed: AddPlayerForConnection handler is now internal to keep safety checks intact
- Changed: A bunch of messages converted to value types for performance

## Version 3.11.6 - 2019-Jul-10

- Fixed: Telepathy IPv4, IPv6, and FQDN should all work now
- Fixed: TelepathyTransport error in UWP builds
- Fixed: OnApplicationQuit is handled better now
- Fixed: Performance and memory allocation improvements
- Changed: Telepathy Source is now included instead of a DLL

## Version 3.10.10 - 2019-Jun-19

- Added: Scene Message now supports params for SceneMode (Normal / Additive) and PhysicsMode (2D / 3D)
- Added: ClientScene.Send now has an optional ChannelId parameter
- Added: ASMDEF to Examples folder
- Added: Support for sending and receiving ArraySegment\<byte\>
- Added: IReadOnlyList\<T\> in SyncLists
- Fixed: NetworkManager not switching to correct scene in some cases
- Fixed: ListServer Ping not found in WebGL
- Fixed: TelepathyTransport.GetMaxPacketSize uses the new configurable max size
- Fixed: Significant reduction in memory allocation and garbage collection
- Changed: Use transform.localPosition and transform.localRotation for better VR support
- Removed: Websockets send queues (they never worked) and SSL (temporarily because it didn't work)

## Version 3.6.7 -- 2019-Apr-28

- Changed: NetworkReader now uses ArraySegment\<byte\> to minimize allocations.

## Version 3.6.6 -- 2019-Apr-24

- Fixed: Reverted two internal refactor commits that had unintended consequences.

## Version 3.6.5 -- 2019-Apr-23

- Fixed: Unity 2019.1 compatibility
- Fixed: Erroneous error indicating prefabs were missing Scene ID's
- Fixed: OnDeserializeSafely now works without allocations
- Fixed: Weaver not writing symbol files, preventing breakpoints from working in Visual Studio
- Fixed: NetworkIdentity SceneID generation now uses RNG Crypto Service Provider 
- Fixed: Scene lighting in Additive example
- Fixed: Player Prefab transform details now respected when instantiated in the absence of NetworkStartPosition
- Removed: Tests folder from Unity package generation (no end-user value)

## Version 3.5.9 -- 2019-Apr-12

- Fixed: NetworkManager round-robin mode using NetworkStartPosition objects now uses hierarchy sibling order.
- Fixed: IsLocalPlayer is now reliably accurate in `Start()` by combining OwnerMessage and SpawnPrefabMessage
- Fixed: Stack overflow issues with Weaver and Cecil
- Fixed: Corrected assembly paths passed to weaver
- Fixed: Enum bandwdith reduced in SyncVars, Commands, and Rpcs
- Fixed: Placeholder files added for removed code files so updating works better
- Changed: NetworkManager `isHeadless` is a static property now, changed from `IsHeadless()`.

## Version 3.5.3 -- 2019-Apr-10

- Fixed: Exceptions in overrides of Network Manager and other components incorrectly silenced.
- Fixed: Lobby system sometimes would not spawn and swap game player prefabs into the game scene
- Fixed: Application.targetFrameRate no longer set in host mode
- Changed: Telepathy: Split MaxMessageSize to allow setting a different value for client and server

## Version 3.4.9 -- 2019-Apr-6

- Added: Semantic Versioning (which is why we jumped from 1.6 to 3.4.9)
- Added: [SyncDictionary](../Classes/SyncDictionary)
- Added: [SyncHashSet](../Classes/SyncHashSet)
- Added: [SyncSortedSet](../Classes/SyncSortedSet)
- Added: SyncList and SyncDictionary support all IList and IDictionary derived types, respectively
- Added: Documentation for [SyncVars](../Classes/SyncVars)
- Added: Documentation for [SyncEvents](../Classes/SyncEvent)
- Added: NoRotation to NetworkTransform
- Added: Scale is now included in spawn payload along with position and rotation
- Added: Generic `IMessageBase` to allow struct message types
- Added: Weaver now supports Vector2Int and Vector3Int
- Added: List Server example
- Added: Additive Scenes example
- Fixed: SyncLists now work correctly for primitives and structs
- Fixed: SyncVar Hooks now will update the local property value after the hook is called  
  - You no longer need to have a line of code in your hook method to manualy update the local property.
- Fixed: Host should not call Disconnect on transports
- Fixed: NetworkAnimimator now supports up to 64 animator parameters
- Fixed: NetworkManager `StartServer` no longer assumes scene zero is the default scene...uses `GetActiveScene` now
- Fixed: NetworkServer `Shutdown` now resets `netId` to zero
- Fixed: Observers are now properly rebuilt when client joins and `OnRebuildObservers` / `OnCheckObserver` is overridden
- Fixed: NetworkProximityChecker: On rare occasion, player could be excluded from observers rebuild
- Fixed: NetworkLobbyPlayer `OnClientReady` works now
- Fixed: NetworkLobbyPlayer Remove button not showing for P1 when Server Only
- Fixed: NetworkLobbyManager `pendingPlayers` and `lobbySlots` lists are now public for inheritors
- Fixed: Offline scene switching now works via `StopClient()`
- Fixed: Pong example updated
- Fixed: Source Weaver was deleting PDB files, preventing breakpoints and debugging from working.
- Changed: TargetRpc NetworkConnection paramater is now optional...the calling client's NetworkConnection is default
- Changed: Movement example replaced with Tank example
- Changed: NetworkClient functions are all static now, so the singleton is gone.  Use NetworkClient directly.
- Changed: SyncList now supports structs directly, making SyncListSTRUCT obsolete.
- Removed: SyncListSTRUCT - Use SyncList instead.
- Removed: NetworkClient.ShutdownAll is obsolete -- Use NetworkClient.Shutdown instead

## Version 1.6 -- 2019-Mar-14

- Fixed: Websockets transport moved to Mirror.Websocket namespace
- Fixed: NetworkAnimator bandwidth abuse
- Fixed: NetworkAnimator float sync bug
- Fixed: Persistent SceneID's for Networked objects
- Changed: Documentation for [Transports](../Transports)
- Changed: Weaver is now full source...FINALLY!
- Changed: ClientScene.AddPlayer 2nd parameter is now `byte[] extraData` instead of `MessageBase extraMessage` 
    - Please refer to the code sample [here](../Concepts/Authentication) to see how to update your code.
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
