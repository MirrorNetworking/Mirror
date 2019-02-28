# Change Log

## Version 1.5 -- 2019-Mar-01

- Added: Lobby Manager and Lobby Player with example game
- Added: Unit tests for Weaver
- Added: Configurable Server Tickrate in NetworkManager
- Added: New virtual OnClientChangeScene fires right before SceneManager.LoadSceneAsync is executed
- Fixed: Garbage allocations removed from a lot of things (more work to do, we know)
- Fixed: SyncVar hook not firing when clients joined
- Fixed: NetworkManager no longer assumes it's on Scene(0) in Build Settings
- Fixed: NetworkAnimator no longer lmited to 6 variables
- Fixed: TelepathyTransport delivering messages when disabled
- Removed: SceneAttribute.cs (merged to CustomAttributes.cs)
- Removed: NetworkClient.allClients (Use NetworkClient.singleton instead)
- Removed: NetworkServer.hostId and NetworkConnection.hostId (holdovers from LLAPI)
- Removed: NetworkConnection.isConnected (NetworkConnection is always connected)
- Changed: Minimum Unity version: 2018.3.6


## Version 1.4 -- 2019-Feb-01

- Mirror is now full source - no more DLL's
- Transports are now components: TCP, UDP, WebGL, Steam


## Version 1.3 -- 2019-Jan-01


