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
-   [SyncLists](SyncLists)  
    SyncLists contain lists of values:
	-   SyncListString
	-   SyncListFloat
	-   SyncListInt
	-   SyncListUInt
	-   SyncListBool
