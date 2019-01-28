# Network Clients and Servers

Many multiplayer games can use the Network Manager to manage connections, but you can also use the lower-level NetworkServer and NetworkClient classes directly.

When using the High-Level API, every game must have a host server to connect to. Each participant in a multiplayer game can be a client, a dedicated server, or a combination of server and client at the same time. This combination role is the common case of a multiplayer game with no dedicated server.

For multiplayer games with no dedicated server, one of the players running the game acts as the server for that game. That player’s instance of the game runs a “local client” instead of a normal remote client. The local client uses the same Scenes and GameObjects as the server, and communicates internally using message queues instead of sending messages across the network. To Mirror code and systems, the local client is just another client, so almost all user code is the same, whether a client is local or remote. This makes it easy to make a game that works in both multiplayer and standalone mode with the same code.

A common pattern for multiplayer games is to have a GameObject that manages the network state of the game. Below is the start of a NetworkManager script. This script would be attached to a GameObject that is in the start-up Scene of the game. It has a simple UI and keyboard handling functions that allow the game to be started in different network modes. Before you release your game you should create a more visually appealing menu, with options such as “Start single player game” and “Start multiplayer game”.

```
using UnityEngine;
using Mirror;

public class MyNetworkManager : MonoBehaviour {
    public bool isAtStartup = true;
    NetworkClient myClient;

    void Update () 
    {
        if (isAtStartup)
        {
            if (Input.GetKeyDown(KeyCode.S))
                SetupServer();

            if (Input.GetKeyDown(KeyCode.C))
                SetupClient();

            if (Input.GetKeyDown(KeyCode.B))
            {
                SetupServer();
                SetupLocalClient();
            }
        }
    }
    void OnGUI()
    {
        if (isAtStartup)
        {
            GUI.Label(new Rect(2, 10, 150, 100), "Press S for server");     
            GUI.Label(new Rect(2, 30, 150, 100), "Press B for both");       
            GUI.Label(new Rect(2, 50, 150, 100), "Press C for client");
        }
    }   
}
```

This basic code calls setup functions to get things going. Below are the simple setup functions for each of the scenarios. These functions create a server, or the right kind of client for each scenario. Note that the remote client assumes the server is on the same machine (127.0.0.1). For a finished game this would be an internet address, or something supplied by the Matchmaking system.

```
// Create a server and listen on a port
public void SetupServer()
{
    NetworkServer.Listen(4444);
    isAtStartup = false;
}

// Create a client and connect to the server port
public void SetupClient()
{
    myClient = new NetworkClient();
    myClient.RegisterHandler(MsgType.Connect, OnConnected);     
    myClient.Connect("127.0.0.1");
    isAtStartup = false;
}

// Create a local client and connect to the local server
public void SetupLocalClient()
{
    myClient = ClientScene.ConnectLocalServer();
    myClient.RegisterHandler(MsgType.Connect, OnConnected);     
    isAtStartup = false;
}
```

The clients in this code register a callback function for the connection event [MsgType.Connect](https://docs.unity3d.com/ScriptReference/Networking.MsgType.Connect.html). This is a built-in message of Mirror that the script invokes when a client connects to a server. In this case, the code for the handler on the client is:

```
// client function
    public void OnConnected(NetworkMessage netMsg)
    {
        Debug.Log("Connected to server");
    }
```

This is enough to get a multiplayer application up and running. With this script you can then send network messages using NetworkClient.Send and NetworkServer.SendToAll. Note that sending messages is a low level way of interacting with the system.
