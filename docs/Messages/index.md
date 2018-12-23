# Messages Overview

General description of Messages

-   **AddPlayerMessage**  
    This is passed to handler functions registered for the AddPlayer built-in message.
-   **EmptyMessage**  
    A utility class to send a network message with no contents.

```cs
    using UnityEngine;
    using Mirror;
    
    public class Test
    {
        void SendNotification()
        {
            var msg = new EmptyMessage();
            NetworkServer.SendToAll(667, msg);
        }
    }
```

-   **ErrorMessage**  
    This is passed to handler functions registered for the SYSTEM_ERROR built-in message.
-   **IntegerMessage**  
    A utility class to send simple network messages that only contain an integer.

```cs
    using UnityEngine;
    using Mirror;
    
    public class Test
    {
        void SendValue(int value)
        {
            var msg = new IntegerMessage(value);
            NetworkServer.SendToAll(MsgType.Scene, msg);
        }
    }
```

-   **NotReadyMessage**  
    This is passed to handler functions registered for the SYSTEM_NOT_READY built-in message.
-   **PeerAuthorityMessage**  
    Information about a change in authority of a non-player in the same network game.  
    This information is cached by clients and used during host-migration.
-   **PeerInfoMessage**  
    Information about another participant in the same network game.  
    This information is cached by clients and used during host-migration.
-   **PeerInfoPlayer**  
    A structure used to identify player object on other peers for host migration.
-   **PeerListMessage**  
    Internal UNET message for sending information about network peers to clients.
-   **ReadyMessage**  
    This is passed to handler functions registered for the SYSTEM_READY built-in message.
-   **ReconnectMessage**  
    This network message is used when a client reconnect to the new host of a game.
-   **RemovePlayerMessage**  
    This is passed to handler funtions registered for the SYSTEM_REMOVE_PLAYER built-in message.
-   **StringMessage**  
    This is a utility class for simple network messages that contain only a string.  
    This example sends a message with the name of the Scene.

```cs
    using UnityEngine;
    using Mirror;
    
    public class Test
    {
        void SendSceneName(string sceneName)
        {
            var msg = new StringMessage(sceneName);
            NetworkServer.SendToAll(MsgType.Scene, msg);
        }
    }
```
