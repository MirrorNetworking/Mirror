# Network Manager Callbacks

There are a number of events that can occur over the course of the normal operation of a multiplayer game, such as the host starting up, a player joining, or a player leaving. Each of these possible events has an associated callback that you can implement in your own code to take action when the event occurs.

To do this for the Network Manager, you need to create your own script which inherits from NetworkManager. You can then override the virtual methods on NetworkManager with your own implementation of what should happen when the given event occurs.

This page lists all the virtual methods (the callbacks) that you can implement on the Network Manager, and when they occur. The callbacks that occur, and the order they occur, vary slightly depending on whether your game is running in LAN mode, so each mode’s callbacks are listed separately below.

## LAN Callbacks

These are the callbacks that occur when the game is running on a Local Area Connection (LAN). A game can be running in one of three modes, host, client, or server-only. The callbacks for each mode are listed below:

### LAN callbacks in host mode:

When the host is started:

-   `Start()` function is called
-   `OnStartHost`
-   `OnStartServer`
-   `OnServerConnect`
-   `OnStartClient`
-   `OnClientConnect`
-   `OnServerSceneChanged`
-   `OnServerReady`
-   `OnServerAddPlayer`
-   `OnClientChangeScene`
-   `OnClientSceneChanged`

When a client connects:

-   `OnServerConnect`
-   `OnServerReady`
-   `OnServerAddPlayer`

When a client disconnects:

-   `OnServerDisconnect`

When the host is stopped:

-   `OnStopHost`
-   `OnStopServer`
-   `OnStopClient`

### LAN callbacks in client mode

When the client starts:

-   `Start()` function is called
-   `OnStartClient`
-   `OnClientConnect`
-   `OnClientChangeScene`
-   `OnClientSceneChanged`

When the client stops:

-   `OnStopClient`
-   `OnClientDisconnect`

### LAN callbacks in server mode

When the server starts:

-   `Start()` function is called
-   `OnStartServer`
-   `OnServerSceneChanged`

When a client connects:

-   `OnServerConnect`
-   `OnServerReady`
-   `OnServerAddPlayer`

When a client disconnects:

-   `OnServerDisconnect`

When the server stops:

-   `OnStopServer`
