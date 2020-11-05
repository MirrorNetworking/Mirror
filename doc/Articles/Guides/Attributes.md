# Attributes Overview

Networking attributes are added to member functions of NetworkBehaviour scripts, to make them run on either the client or server.

These attributes can be used for Unity game loop methods like Start or Update, as well as other implemented methods.

> **NOTE**: when using abstract or virtual methods the Attributes need to be applied to the override methods too.

-   **<xref:Mirror.ServerAttribute>**  
    Only a server can call the method (throws an error when called on a client unless you specify error = false).
-   **<xref:Mirror.ClientAttribute>**  
    Only a Client can call the method (throws an error when called on the server unless you specify error = false).
-   **<xref:Mirror.ClientRpcAttribute>**  
    The server uses a Remote Procedure Call (RPC) to run that function on clients. It has a **target** option allowing you to specify in which clients it should be executed, along with a **channel** option. See also: [Remote Actions](Communications/RemoteActions.md)
-   **<xref:Mirror.ServerRpcAttribute>**  
    Call this from a client to run this function on the server. Make sure to validate input etc. It's not possible to call this from a server. Use this as a wrapper around another function, if you want to call it from the server too. Note that you can return value from it. See also [Remote Actions​](Communications/RemoteActions.md)  
    
    The allowed argument types are:

    -   Basic type (byte, int, float, string, UInt64, etc)

    -   Built-in Unity math type (Vector3, Quaternion, etc),

    -   Arrays of basic types

    -   Structs containing allowable types

    -   NetworkIdentity

-   **SyncVar**  
    [SyncVars](Sync/SyncVars.md) are used to synchronize a variable from the server to all clients automatically. Don't assign them from a client, it's pointless. Don't let them be null, you will get errors. You can use int, long, float, string, Vector3 etc. (all simple types) and NetworkIdentity. You can use [hooks](Sync/SyncVarHook.md).
