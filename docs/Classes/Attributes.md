# Attributes Overview

Networking attributes are added to member functions of NetworkBehaviour scripts, to make them run on either the client or server.

These attributes can be used for Unity game loop methods like Start or Update, as well as other implemented methods.

-   **NetworkSettings**  
    Something about this
-   **Server**  
    means don't allow a client to call that method (throws a warning or an error when called on a client).
-   **ServerCallback**  
    Something about this
-   **Client**  
    means don't allow a server to call that method (throws a warning or an error when called on the server).
-   **ClientRpc**  
    The server uses an Rpc to run that function on clients.
-   **ClientCallback**  
    Something about this
-   **TargetRpc**  
    Something about this
-   **Command**  
	Call this from a client to run this function on the server. Make sure to validate input etc. It's not possible to call this from a server. Use this as a wrapper around another function, if you want to call it from the server too.
	The allowed argument types are;
	-   Basic type (byte, int, float, string, UInt64, etc)
	-   Built-in Unity math type (Vector3, Quaternion, etc),
	-   Arrays of basic types
	-   Structs containing allowable types
	-   NetworkIdentity
	-   NetworkInstanceId
	-   NetworkHash128
	-   GameObject with a NetworkIdentity component attached.
-   **SyncVar**  
	SyncVars are used to synchronize a variable from the server to all clients automatically. Don't assign them from a client, it's pointless. Don't let them be null, you will get errors. You can use int, long, float, string, Vector3 etc. (all simple types) and NetworkIdentity and GameObject if the GameObject has a NetworkIdentity attached to it. You can use [hooks](SyncVarHook).
-   **SyncEvent**  
    Something about this
