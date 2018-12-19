# Command

Call this from a client to run this function on the server. Make sure to
validate input etc. It's not possible to call this from a server. Use this as a
wrapper around another function, if you want to call it from the server too.

The allowed argument types are;

-   Basic type (byte, int, float, string, UInt64, etc)

-   Built-in Unity math type (Vector3, Quaternion, etc),

-   Arrays of basic types

-   Structs containing allowable types

-   NetworkIdentity

-   NetworkInstanceId

-   NetworkHash128

-   GameObject with a NetworkIdentity component attached.
