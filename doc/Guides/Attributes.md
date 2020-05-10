# Attributes Overview

Networking attributes are added to member functions of NetworkBehaviour scripts, to make them run on either the client or server.

These attributes can be used for Unity game loop methods like Start or Update, as well as other implemented methods.
-   **NetworkSettings**  
    This attribute has been deprecated because `channels` were moved to transports (where applicable) and `interval` was moved to an inspector property
-   **Server**  
    Only a server can call the method (throws a warning or an error when called on a client).
-   **ServerCallback**  
    Same as **Server** but does not throw warning when called on client.
-   **Client**  
    Only a Client can call the method (throws a warning or an error when called on the server).
-   **ClientCallback**  
    Same as **Client** but does not throw warning when called on server.
-   **ClientRpc**  
    The server uses a Remote Procedure Call (RPC) to run that function on clients. See also: [Remote Actions](Communications/RemoteActions.md)
-   **TargetRpc**  
    This is an attribute that can be put on methods of NetworkBehaviour classes to allow them to be invoked on clients from a server. Unlike the ClientRpc attribute, these functions are invoked on one individual target client, not all of the ready clients. See also: [Remote Actions](Communications/RemoteActions.md)
-   **Command**  
    Call this from a client to run this function on the server. Make sure to validate input etc. It's not possible to call this from a server. Use this as a wrapper around another function, if you want to call it from the server too. See also [Remote Actions​](Communications/RemoteActions.md)  
    The allowed argument types are:

    -   Basic type (byte, int, float, string, UInt64, etc)

    -   Built-in Unity math type (Vector3, Quaternion, etc),

    -   Arrays of basic types

    -   Structs containing allowable types

    -   NetworkIdentity

    -   Game object with a NetworkIdentity component attached.
-   **SyncVar**  
    [SyncVars](Sync/SyncVars.md) are used to synchronize a variable from the server to all clients automatically. Don't assign them from a client, it's pointless. Don't let them be null, you will get errors. You can use int, long, float, string, Vector3 etc. (all simple types) and NetworkIdentity and game object if the game object has a NetworkIdentity attached to it. You can use [hooks](Sync/SyncVarHook.md).
-   **SyncEvent**  
    [SyncEvent](Sync/SyncEvent.md) are networked events like ClientRpc's, but instead of calling a function on the game object, they trigger Events instead.
