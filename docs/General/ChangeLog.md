# Change Log

## Version 1.5 -- 2019-Mar-01

- Added: Lobby Manager and Lobby Player with example game
- Fixed: SyncVar hook not firing when clients joined


## Version 1.4 -- 2019-Feb-01

- Mirror is now full source - no more DLL's
- Transports are now components: TCP, UDP, WebGL, Steam
- Added: HelpURL attirbutes to components
- Added: Automatic targetFramerate for headless builds
- Added: ByteMessage to Messages class
- Fixed: Connectiing state can be cancelled properly
- Fixed: NetworkTransformBase interpolation applied to client's own object
- Fixed: Objects are spawned with correct rotation
- Fixed: SceneId assignment
- Fixed: Changed syncInterval wasn't saved...it is now
- Fixed: Additive Scene loading
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
