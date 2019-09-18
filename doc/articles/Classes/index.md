# Classes Overview

Mirror includes the following classes:
-   [NetworkServer](../../api/Mirror.NetworkServer.html)  
    Network Server is a High-Level-API class that manages connections from multiple clients.
-   [NetworkClient](../../api/Mirror.NetworkClient.html)  
    Network Client is a high-level API class that manages a network connection from a client to a server, and can send and receive messages between the client and the server.
-   [NetworkConnection](../../api/Mirror.NetworkConnecion.html)  
    Network Connection is a high-level API class that encapsulates a network connection.
-   [NetworkBehaviour](NetworkBehaviour.md)  
    Network Behaviour scripts work with game objects that have a NetworkIdentity component. These scripts can perform high-level API functions such as Commands, ClientRpc’s, SyncEvents and SyncVars.
-   [Attributes](Attributes.md)  
    Networking attributes are added to member functions of NetworkBehaviour scripts, to make them run on either the client or server.
-   [SyncVars](SyncVars.md)  
    SyncVars are variables of scripts that inherit from NetworkBehaviour, which are synchronized from the server to clients.
-   [SyncEvents](SyncEvent.md)  
    SyncEvents are networked events like ClientRpc’s, but instead of calling a function on the game object, they trigger Events instead.
-   [SyncLists](SyncLists.md)  
    SyncLists contain lists of values and synchronize data from servers to clients.
-   [SyncDictionary](SyncDictionary.md)  
    A SyncDictionary is an associative array containing an unordered list of key, value pairs.
-   [SyncHashSet](SyncHashSet.md)  
    An unordered set of values that do not repeat.
-   [SyncSortedSet](SyncSortedSet.md)  
    A sorted set of values tha do not repeat.
