# Classes Overview

General description of Classes

-   [NetworkServer](NetworkServer)  
    NetworkServer is a High-Level-API class that manages connections from multiple clients.
-   [NetworkClient](NetworkClient)  
    NetworkClient is a high-level API class that manages a network connection from a client to a server, and can send and receive messages between the client and the server.
-   [NetworkConnection](NetworkConnection)  
    NetworkConnection is a high-level API class that encapsulates a network connection.
-   [NetworkBehavior](NetworkBehavior)  
    NetworkBehaviour scripts work with GameObjects that have a NetworkIdentity component. These scripts can perform high-level API functions such as Commands, ClientRPCs, SyncEvents and SyncVars.
-   [Attributes](Attributes)  
	Networking attributes are added to member functions of NetworkBehaviour scripts, to make them run on either the client or server.
-   [SyncVars](SyncVars)  
    SyncVars are variables of scripts that inherit from NetworkBehaviour, which are synchronized from the server to clients. 
-   [SyncEvents](SyncEvent)  
    SyncEvents are networked events like ClientRpc’s, but instead of calling a function on the GameObject, they trigger Events instead.
-   [SyncLists](SyncLists)  
    SyncLists contain lists of values and synchronize data from servers to clients.
-   [SyncDictionary](SyncDictionary)  
    A SyncDictionary is an associative array containing an unordered list of key, value pairs.
-   [SyncHashSet](SyncHashSet)  
    An unordered set of values that do not repeat.
-   [SyncSortedSet](SyncSortedSet)  
    A sorted set of values tha do not repeat.
