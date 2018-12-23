# Attributes Overview

Networking attributes are added to member functions of NetworkBehaviour scripts, to make them run on either the client or server.

These attributes can be used for Unity game loop methods like Start or Update, as well as other implemented methods.

-   [NetworkSettings](NetworkSettings)  
    Something about this

-   [Server](Server)  
    means don't allow a client to call that method (throws a warning or an error when called on a client).

-   [ServerCallback](ServerCallback)  
    Something about this

-   [Client](Client)  
    means don't allow a server to call that method (throws a warning or an error when called on the server).

-   [ClientRpc](ClientRpc)  
    The server uses an Rpc to run that function on clients.

-   [ClientCallback](ClientCallback)  
    Something about this

-   [TargetRpc](TargetRpc)  
    Something about this

-   [Command](Command)  
    Call this from a client to run this function on the server.

-   [SyncVar](SyncVar)  
    SyncVars are used to synchronize a variable from the server to all clients automatically.

-   [SyncEvent](SyncEvent)  
    Something about this
