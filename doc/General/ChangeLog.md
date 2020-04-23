# Change Log

**Mirror is published to the [Asset Store](https://assetstore.unity.com/packages/tools/network/mirror-129321) at the start of every month, unless some critical issue causes a delay.**

Mirror uses semantic versioning, and the versions shown here are those that were published to the Asset Store, and occasionally major version bumps happen mid-month between store submissions and are therefore not individually shown here.

## Version 12.x.x -- In Progress
- Added: [NetworkMatchChecker](../Components/NetworkMatchChecker.md) component. Use this component to create many concurrent isolated matches within a single game server.
- Added: [SyncLists](../Guides/Sync/SyncLists.md) now have Find and FindAll functions.
- Added: NetworkBehaviour now has OnStopServer and OnStopClient virtual methods
- Added: Weaver now supports custom Reader & Writer for types in other assemblies
- Added: Network Manager now has an optional setting to check for and disconnect remote connections that have gone silent for a specified interval.
- Fixed: NetworkAnimator no longer double-fires SetTrigger / ResetTrigger on the host client
- Fixed: Destroy is no longer invoked twice on the server for the player object.
- Changed: NetworkBehaviour: `OnNetworkDestroy` was renamed to `OnStopClient`.
- Changed: **Breaking** NetworkBehaviour: `OnRebuildObservers`, `OnCheckObserver`, and `OnSetHostVisibility` were moved to a separate class called `NetworkVisibility`

## Version 11.4.2 - 2020-Apr-03
- Added: SyncVar hooks can be virtual now, and overriden in a derived class.
- Added: Virtual OnRoomStopServer to NetworkRoomManager and Script Template
- Added: 10K Networked Objects Benchmark Example
- Fixed: Setting breakpoints in an IDE for Command's and Rpc's work correctly now.
- Fixed: NetworkServer's calls to SendToObservers now reports correct channel to Mirror Profiler.
- Fixed: NetworkRoomManager's `roomPlayerPrefab` is now `protected` so it can be accessed in derived classes.
- Fixed: NetworkRoomPlayer inspector and documentation updated to be less confusing.
- Fixed: NetworkIdentity no longer double calls NetworkServer.Destroy.
- Fixed: NetworkAnimator now correctly excludes parameters controlled by curves.
- Fixed: NetworkBehaviour now uses a property drawer for the SyncVar label so it displays better.
- Fixed: NetworkServer.SendToReady overloads are no longer ambiguous.
- Fixed: NetworkRoomManager no longer incorrectly destroys the game player object. It's left in the game scene to be cleaned up by Unity when the scene changes.
- Fixed: StopHost correctly raises OnServerDisconnect in Network Manager, and correctly unwinds before shutting down the server.
- Fixed: `isServer` is no longer incorrectly false on server in Network Identity's OnDestroy
- Changed: Network Manager `offlineScene` and `onlineScene` store full paths now, so use SceneManager.GetActiveScene().path instead.
- Changed: Network Manager HUD now calls StopHost / StopServer / StopClient more appropriately.
- Changed: Network Manager HUD labels no longer say LAN. Associated docs also cleaned up to eliminate the misconception of Mirror being LAN only solution.
- Changed: NetworkTransform compression removed and message handling is much simpler now.
- Changed: NetworkSceneChecker initializes in Awake again because OnEnable proved to be unreliable in some cases.
- Changed: Network Manager will no longer lose references to scenes if they aren't in the Build Settings scene list, however moving or renaming scenes may cause references to be lost.
- Changed: **Breaking** Many obsolete methods and properties removed. Use version 10 first if upgrading from UNet or older Mirror. See [Deprecations](Deprecations.md) for complete list.

## Version 10.4.7 - 2020-Mar-03
- Added: Weaver will now block play mode and builds if there are weaver errors and show them in the console again.
- Added: PooledNetworkReader and PooledNetworkWriter, both Disposable.
- Added: NetworkReader.ReadMessage<T>.
- Added: NetworkDiscovery now handles headless server mode.
- Added: SyncVar, Cmd's and Rpc's now support Scriptable Objects via CreateInstance on the receiving side.
- Added: [Discord Transport](../Transports/Discord.md)
- Fixed: `isClient` now returns true on clients in OnDestroy for networked objects.
- Fixed: Host Player race condition for Ready message.
- Fixed: NetworkAnimator and NetworkTransform now correctly check for client authority in their respective Command methods.
- Fixed: Network Room Manager Script Template had a virtual method instead of an override.
- Fixed: NetworkServer's calls to NetworkConnectionToClient.Send now includes the channelId parameter that was missing.
- Fixed: NetworkServer's calls to SendToAll, SendToReady, and SendToObservers send to the exact connection if it is detected as local connection, instead of falling back to the .localConnection.
- Fixed: NetworkServer.SpawnObjects returns false if server isn't running.
- Fixed: NetworkTransform rotation detection improved.
- Fixed: Weaver generated code now builds properly for IL2CPP (again).
- Changed: StartHost in Network Manager is no longer a virtual method (and shoudn't have been). Override OnStartHost instead.
- Changed: NetworkRoomManager's OnRoomServerSceneLoadedForPlayer now includes NetworkConnection parameter.
- Changed: NetworkSceneChecker now works from OnEnable instead of Awake, and uses Scene instead of scene name.
- Changed: Renamed NeworkWriter.Write to WriteMessage for consistency.

## Version 9.0.2 - 2020-Feb-04
- Added: NetworkAnimator now has a ResetTrigger function and server / client authority warnings.
- Added: NetworkTransform now has 3 new floats for Sensitivity to quiet down message traffic from micro changes.
- Added: Network Observer added to [Script Templates](ScriptTemplates.md) -- See the new Mirror section in the Assets > Create menu.
- Added: [Network Discovery](../Components/NetworkDiscovery.md) has been reimplemented including an example and script template -- thanks to all those who contributed!
- Added: [Network Discovery](../Guides/NetworkDiscovery.md) Guide added to documentation.
- Added: [Network Scene Checker Component](../Components/NetworkSceneChecker.md).
- Added: Mirror Icon for all components.
- Added: Inspector Headers to NetworkBehaviour, NetworkTransform, and NetworkAnimator.
- Added: URI added to supported data types.
- Added: `NetworkReaderPool` has been implemented in place of `new NetworkReader` everywhere, significantly reducing garbage allocation.
- Fixed: NetworkTransform and NetworkAnimator now uses NetworkWriterPool.
- Fixed: NetworkTransform and NetworkAnimator now respect `hasAuthority` for client owned objects.
- Fixed: NetworkTransform will now correctly teleport if time / distance are too large.
- Fixed: NetworkAnimator now correctly syncs when clientAuthority is false.
- Fixed: Client owned objects are now destroyed when client disconnects (regression).
- Fixed: Client authority is now preserved for networked objects carried through scene change in DontDestroyOnLoad (DDOL).
- Fixed: Starting server immediately after cancelling client connection attempt no longer throws a NRE.
- Fixed: IL2CPP builds no longer trigger an assert when stopping server.
- Fixed: SyncVars are now set for Host player before hook is invoked.
- Fixed: SyncDictionary now correctly updates the value before invoking callbacks.
- Fixed: StopHost no longer tries to change to the Offline scene twice.
- Fixed: NetworkRoomManager roomSlots are now correctly managed on both server and clients.
- Fixed: NetworkRoomManager now correctly supports multiple GamePlay scenes and adds a virtual `OnRoomServerAddPlayer`.
- Fixed: Additive scene operations no longer incorrectly lead to extraneous AddPlayer messages from clients.
- Fixed: `NetworkWriterPool` is now used everywhere instead of `new NetworkWriter` (we missed a couple when the pool was implemented).
- Fixed: Patch for Unity 2019.3.x `RequestScriptReload` was moved to EditorUtility from UnityEditorInternal.InternalEditorUtility.
- Fixed: Arrays of custom types are now correctly supported.
- Changed: Shutdown logic has been streamlined.
- Changed: NetworkIdentity `GetSceneIdenity` method renamed to `GetSceneIdentity` (name typo).
- Changed: OnApplicationQuit virtual method added to Transport class and `Transport.activeTransport.Shutdown()` is no longer called from Network Manager.
- Changed: **Breaking** SyncVar Hooks now require **two** parameters, one each for the old and new value, and the property value is now set **before** the hook is called.

## Version 6.7.7 - 2020-Jan-01
- Added: [Script Templates](ScriptTemplates.md) -- See the new Mirror section in the Assets > Create menu.
- Added: Full Text Search added to docs.
- Added: Basic Chat example.
- Added: Some youtube videos have been created and linked from various doc pages where appropriate.
- Added: Transports can now support using a URI for connection including port.
- Added: version.txt file is now included with the release version in it.
- Added: Structs that inherit from IMessageBase now generate serializers.
- Fixed: Components now appear in docs under API Reference.
- Fixed: Delayed disconnect in Basic Authenticator.
- Fixed: Multiplexer now handles OnClientConnect and GetMaxMessageSize correctly.
- Fixed: NetworkRoomManager OnRoomServerCreateGamePlayer now includes the Room Player Object as parameter.
- Changed: Room example now has NetworkRoomManager in the Offline scene and correctly switches to the Room scene when server / client is started.
- Changed: NetworkManager startPositionIndex and loadingSceneAsync are now public.
- Changed: SceneMessage now has an optional customHandling flag so devs can handle their own custom scene loading, e.g. using Addressables.
- Changed: **Breaking** NetworkTransform now defaults to server authority and has a Client Authority checkbox.

## Version 6.3.0 - 2019-Dec-09
- Added: SyncMode selector now works for components on any scene objects owned by a client in addition to the player object, e.g. pets.
- Added: MessageBase now fully supports inheritance.
- Added: Room example now has UI button for host to return all clients to the Room scene and other improvements.
- Fixed: ReplacePlayerForConnection now works for existing scene objects as long as another player doesn't own the targetted object.
- Fixed: isClient and isServer are now true for networked objects in Start and OnStartClient / OnStartServer, respectively.
- Fixed: hasAuthority is now set before OnStartClient and OnStartLocalPlayer are invoked.
- Changed: connectionToClient is now used to assign client authority.
- Changed: In many respects, the Host player acts a lot more like a client and will reveal bugs better.
- Changed: ReplacePlayerForConnection now has an optional bool to retain authority of the previous object (default = false).
- Removed: NetworkServer.SpawnWithClientAuthority is deprecated in favor of overloads of NetworkServer.Spawn.

## Version 5.0.2 - 2019-Nov-03
- Added: SyncList and SyncSet custom Equality Comparer support.
- Added: Custom serializers may be implemented for any type in Cmd's and Rpc's.
- Added: [Fallback Transport](../Transports/Fallback.md).
- Fixed: SyncVar hooks are no longer called in Host if no change.
- Fixed: NetworkIdentity no longer throws a null reference exception in RemoveClientAuthority.
- Fixed: Server transport now suspended during scene change to prevent erroneous messages.
- Fixed: SyncList, SyncDictionary and SyncSet now use a custom IEnumerator to avoid memory allocation.
- Fixed: sceneID is no longer reset in certain cases when editing a prefab.
- Changed: PreprocessorDefine code moved to CompilerSymbols folder to avoid paradox of missing symbols preventing the symbols being added to the project.
- Changed: Host player no longer gets authority assigned to all objects by default.
- Changed: Commands no longer bypass serialization for Host player, making debugging easier.
- Changed: Local connections now maintain their own message queue.
- Changed: Transport.Available is now abstract.
- Removed: Network Identity: Local Player Authority has been removed as no longer necessary.

## Version 4.0.7 - 2019-Oct-03
- Added: [Authentication](../Guides/Authentication.md) support to authenticate clients in the Connect phase.
- Added: Profiler events. These events can be subscribed to by the [Network Profiler](../Guides/Profiler.md) to provide visual information.
- Added: Transports now include channel in profiler events.
- Added: Transport abstract class now supports sending a message to a list of connection id's.
- Fixed: SceneMessage now has sceneOperation enum so clients can properly handle additive scenes.
- Fixed: NetworkClient handlers are now cleared in Shutdown.
- Fixed: Offline scene is no longer reloaded when client fails to connect or is rejected.
- Fixed: Start Position Index is now reset to zero when server is stopped.
- Fixed: Network Room Players are now all in DontDestroyOnLoad so they don't respawn in the game scene.
- Fixed: Network Room example player controller restores main camera on disable.
- Fixed: Components with different sync intervals were not sending updates to clients.
- Fixed: In certain cases, weaver wouldn't weave some external assemblies.
- Fixed: NetworkAnimator now does a full sync for new clients.
- Fixed: NetworkBehaviour inspector now shows SyncMode for private SyncVars.
- Fixed: Calling Commands and Rpcs of parent classes from inherited classes works as it should.
- Fixed: Telepathy no longer hangs when attempting to connect to a nonexistent host.
- Fixed: Websockets Transport now properly returns the client endpoint information via `ServerGetClientAddress`.
- Fixed: WebGL build now works with ArraySegment.
- Changed: Mirror is now free of garbage memory allocation on the sending side.
    - Some transports may still have a little garbage allocation yet.
- Changed: Deprecated the AddPlayerMessage extraMessage byte\[\] in favor of an easier approach to [Custom Players](../Guides/GameObjects/SpawnPlayerCustom.md).
    - This is a breaking change: The virtual method OnServerAddPlayer loses the AddPlayerMessage parameter.
- Changed: NetworkIdentity.RemoveAuthorityForConnection is now easier to use: no need to supply the current "owner" anymore.
- Changed: Renamed `NetworkConnection.playerController` to `identity` ... see [Deprecations](Deprecations.md) for details.
- Changed: Lobby system renamed to Room to better align the name for what it is and make way for a future Lobby framework.

## Version 3.17.4 - 2019-Sep-04
- Added: Custom Network Readers & Writers via extension methods.
- Added: Network Sync Mode selector on components to sync to observers (default) or just the owner.
- Added: SyncVars now support structs and enums in other assemblies.
- Added: Support for reading and writing array segments.
- Added: NetworkAnimator now has layers support.
- Added: New virtual method OnServerChangeScene to NetworkManager.
- Added: XML summary comments for intellisense and future generated class docs.
- Updated Examples and Documentation.
- Fixed: SceneID was not set to 0 for prefab variants.
- Fixed: Observers were not properly rebuilt on scene changes.
- Fixed: SyncVar hooks were not able to change other SyncVars in Host mode.
- Fixed: Telepathy not setting socket options on accepted clients.
- Fixed: Catch IL2CPP bug.
- Fixed: Telepathy and Websockets now start connections ID's at 1 instead of 2
- Fixed: Websockets support for SSL restored.
- Fixed: NetworkManager no longer complains about missing player prefab if auto-create is disabled.
- Fixed: Removed a garbage allocation in Network Transform.
- Fixed: NetworkClient.Ready was being called unncessarily in certain cases, e.g. SceneMessages.
- Changed: Documentation moved to website and API generated docs implemented.
- Changed: AddPlayerForConnection handler is now internal to keep safety checks intact.
- Changed: A bunch of messages converted to value types for performance.

## Version 3.11.6 - 2019-Jul-10
- Fixed: Telepathy IPv4, IPv6, and FQDN should all work now.
- Fixed: TelepathyTransport error in UWP builds.
- Fixed: OnApplicationQuit is handled better now.
- Fixed: Performance and memory allocation improvements.
- Changed: Telepathy Source is now included instead of a DLL.

## Version 3.10.10 - 2019-Jun-19
- Added: Scene Message now supports params for SceneMode (Normal / Additive) and PhysicsMode (2D / 3D).
- Added: ClientScene.Send now has an optional ChannelId parameter.
- Added: ASMDEF to Examples folder.
- Added: Support for sending and receiving ArraySegment\<byte\>
- Added: IReadOnlyList\<T\> in SyncLists.
- Fixed: NetworkManager not switching to correct scene in some cases.
- Fixed: ListServer Ping not found in WebGL.
- Fixed: TelepathyTransport.GetMaxPacketSize uses the new configurable max size.
- Fixed: Significant reduction in memory allocation and garbage collection.
- Changed: Use transform.localPosition and transform.localRotation for better VR support.
- Removed: Websockets send queues (they never worked) and SSL (temporarily because it didn't work).

## Version 3.6.7 -- 2019-Apr-28
- Changed: NetworkReader now uses ArraySegment\<byte\> to minimize allocations.

## Version 3.6.6 -- 2019-Apr-24
- Fixed: Reverted two internal refactor commits that had unintended consequences.

## Version 3.6.5 -- 2019-Apr-23
- Fixed: Unity 2019.1 compatibility.
- Fixed: Erroneous error indicating prefabs were missing Scene ID's.
- Fixed: OnDeserializeSafely now works without allocations.
- Fixed: Weaver not writing symbol files, preventing breakpoints from working in Visual Studio.
- Fixed: NetworkIdentity SceneID generation now uses RNG Crypto Service Provider.
- Fixed: Scene lighting in Additive example.
- Fixed: Player Prefab transform details now respected when instantiated in the absence of NetworkStartPosition.
- Removed: Tests folder from Unity package generation (no end-user value).

## Version 3.5.9 -- 2019-Apr-12
- Fixed: NetworkManager round-robin mode using NetworkStartPosition objects now uses hierarchy sibling order.
- Fixed: IsLocalPlayer is now reliably accurate in `Start()` by combining OwnerMessage and SpawnPrefabMessage.
- Fixed: Stack overflow issues with Weaver and Cecil.
- Fixed: Corrected assembly paths passed to weaver.
- Fixed: Enum bandwdith reduced in SyncVars, Commands, and Rpcs.
- Fixed: Placeholder files added for removed code files so updating works better.
- Changed: NetworkManager `isHeadless` is a static property now, changed from `IsHeadless()`.

## Version 3.5.3 -- 2019-Apr-10
- Fixed: Exceptions in overrides of Network Manager and other components incorrectly silenced.
- Fixed: Lobby system sometimes would not spawn and swap game player prefabs into the game scene.
- Fixed: Application.targetFrameRate no longer set in host mode.
- Changed: Telepathy: Split MaxMessageSize to allow setting a different value for client and server.

## Version 3.4.9 -- 2019-Apr-6
- Added: Semantic Versioning (which is why we jumped from 1.6 to 3.4.9).
- Added: [SyncDictionary](../Guides/Sync/SyncDictionary.md).
- Added: [SyncHashSet](../Guides/Sync/SyncHashSet.md).
- Added: [SyncSortedSet](../Guides/Sync/SyncSortedSet.md).
- Added: SyncList and SyncDictionary support all IList and IDictionary derived types, respectively.
- Added: Documentation for [SyncVars](../Guides/Sync/SyncVars.md).
- Added: Documentation for [SyncEvents](../Guides/Sync/SyncEvent.md).
- Added: NoRotation to NetworkTransform.
- Added: Scale is now included in spawn payload along with position and rotation.
- Added: Generic `IMessageBase` to allow struct message types.
- Added: Weaver now supports Vector2Int and Vector3Int.
- Added: List Server example.
- Added: Additive Scenes example.
- Fixed: SyncLists now work correctly for primitives and structs.
- Fixed: SyncVar Hooks now will update the local property value after the hook is called.  
    -   You no longer need to have a line of code in your hook method to manualy update the local property.
- Fixed: Host should not call Disconnect on transports.
- Fixed: NetworkAnimimator now supports up to 64 animator parameters.
- Fixed: NetworkManager `StartServer` no longer assumes scene zero is the default scene...uses `GetActiveScene` now.
- Fixed: NetworkServer `Shutdown` now resets `netId` to zero.
- Fixed: Observers are now properly rebuilt when client joins and `OnRebuildObservers` / `OnCheckObserver` is overridden.
- Fixed: NetworkProximityChecker: On rare occasion, player could be excluded from observers rebuild.
- Fixed: NetworkLobbyPlayer `OnClientReady` works now.
- Fixed: NetworkLobbyPlayer Remove button not showing for P1 when Server Only.
- Fixed: NetworkLobbyManager `pendingPlayers` and `lobbySlots` lists are now public for inheritors.
- Fixed: Offline scene switching now works via `StopClient()`.
- Fixed: Pong example updated.
- Fixed: Source Weaver was deleting PDB files, preventing breakpoints and debugging from working.
- Changed: TargetRpc NetworkConnection paramater is now optional...the calling client's NetworkConnection is default.
- Changed: Movement example replaced with Tank example.
- Changed: NetworkClient functions are all static now, so the singleton is gone. Use NetworkClient directly.
- Changed: SyncList now supports structs directly, making SyncListSTRUCT obsolete.
- Removed: SyncListSTRUCT - Use SyncList instead.
- Removed: NetworkClient.ShutdownAll is obsolete -- Use NetworkClient.Shutdown instead.

## Version 1.6 -- 2019-Mar-14
- Fixed: Websockets transport moved to Mirror.Websocket namespace.
- Fixed: NetworkAnimator bandwidth abuse.
- Fixed: NetworkAnimator float sync bug.
- Fixed: Persistent SceneID's for Networked objects.
- Changed: Documentation for [Transports](../Transports/index.md).
- Changed: Weaver is now full source...FINALLY!
- Changed: ClientScene.AddPlayer 2nd parameter is now `byte[] extraData` instead of `MessageBase extraMessage`.
    -   Please refer to the code sample [here](../Guides/Authentication.md) to see how to update your code.
- Changed: NetworkManager -- Headless Auto-Start moved to `Start()` from `Awake()`.
- Changed: Removed Message ID's for all messages - See [Network Messages](../Guides/Communications/NetworkMessages.md) for details.
    -   Message IDs are now generated automatically based on the message name.  
    -   Previously you would call Send(MyMessage.MsgId, message), now you call Send(message).
- Removed: Documentation for Groove Transport - use Websockets Transport instead.

## Version 1.5 -- 2019-Mar-01
- Added: **Migration Tool** to (mostly) automate conversion from UNet.
- Added: Full support for WebSockets and WebSocketsSecure to replace UNet LLAPI.
- Added: Transport Multiplexer - allows the use of multiple concurrent transports.
- Added: NetworkLobbyManager and NetworkLobbyPlayer with example game.
- Added: Configurable Server Tickrate in NetworkManager.
- Added: New virtual OnClientChangeScene fires right before SceneManager.LoadSceneAsync is executed.
- Added: Unit tests for Weaver.
- Fixed: Garbage allocations removed from a lot of things (more work to do, we know).
- Fixed: NetworkProximityChecker now uses OverlapSphereNonAlloc and OverlapCircleNonAlloc.
- Fixed: SyncVar hook not firing when clients joined.
- Fixed: NetworkManager no longer assumes it's on Scene(0) in Build Settings.
- Fixed: NetworkAnimator no longer lmited to 6 variables.
- Fixed: TelepathyTransport delivering messages when disabled.
- Changed: Minimum Unity version: **2018.3.6**
- Removed: SceneAttribute.cs (merged to CustomAttributes.cs).
- Removed: NetworkClient.allClients (Use NetworkClient.singleton instead).
- Removed: NetworkServer.hostId and NetworkConnection.hostId (holdovers from LLAPI).
- Removed: NetworkConnection.isConnected (NetworkConnection is always connected).
- Removed: Transport.GetConnectionInfo (Use ServerGetClientAddress instead).

## Version 1.4 -- 2019-Feb-01
- Added: HelpURL attirbutes to components.
- Added: Automatic targetFramerate for headless builds.
- Added: ByteMessage to Messages class.
- Fixed: Connectiing state can be cancelled properly.
- Fixed: NetworkTransformBase interpolation applied to client's own object.
- Fixed: Objects are spawned with correct rotation.
- Fixed: SceneId assignment.
- Fixed: Changed syncInterval wasn't saved...it is now.
- Fixed: Additive Scene loading.
- Changed: **Mirror is now full source** -- no more DLL's.
- Changed: **Transports are now components** -- TCP, UDP, WebGL, Steam.
- Changed: Transport class now dispatches Unity Events.
- Changed: NetworkServer.SendToClientOfPlayer uses NetworkIdentity now.
- Changed: NetworkServer.SendToObservers uses NetworkIdentity parameter now.
- Changed: NetworkServer.SendToReady uses NetworkIdentity now.
- Changed: NetworkServer.DestroyPlayerForConnection uses NetworkIdentity.spawned now.
- Changed: NetworkConnection.Dispose uses NetworkIdentity.spawned now.
- Changed: NetworkReader.ReadTransform uses NetworkIdentity.spawned now.
- Changed: NetworkTransform reimplemented -- physics removed, code simplified.
- Removed: NetworkClient.hostPort (port is handled at Transport level).
- Removed: NetworkServer.FindLocalObject (Use NetworkIdentity.spawned[netId] instead).
- Removed: ClientScene.FindLocalObject (Use NetworkIdentity.spawned[netId] instead).
