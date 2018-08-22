![Telepathy Logo](https://i.imgur.com/eUk2rmT.png)

[![Build status](https://img.shields.io/appveyor/ci/vis2k73562/telepathy.svg)](https://ci.appveyor.com/project/vis2k73562/telepathy/)
[![AppVeyor tests branch](https://img.shields.io/appveyor/tests/vis2k73562/telepathy.svg)](https://ci.appveyor.com/project/vis2k73562/telepathy/branch/master/tests)
[![Discord](https://img.shields.io/discord/343440455738064897.svg)](https://discordapp.com/invite/N9QVxbM)
[![Codecov](https://codecov.io/gh/vis2k/telepathy/graph/badge.svg)](https://codecov.io/gh/vis2k/telepathy)

Simple, message based, MMO Scale TCP networking in C#. And no magic.

Telepathy was designed with the [KISS Principle](https://en.wikipedia.org/wiki/KISS_principle) in mind.<br/>
Telepathy is fast and extremely reliable, designed for [MMO](https://www.assetstore.unity3d.com/#!/content/51212) scale Networking.<br/>
Telepathy uses framing, so anything sent will be received the same way.<br/>
Telepathy is raw C# and can be used in Unity3D too.<br/>

# What makes Telepathy special?
Telepathy was originally designed for [uMMORPG](https://www.assetstore.unity3d.com/#!/content/51212) after 3 years in UDP hell.

We needed a library that is:
* Stable & Bug free: Telepathy uses only 400 lines of code. There is no magic.
* High performance: Telepathy can handle thousands of connections and packages.
* Concurrent: Telepathy uses one thread per connection. It can make heavy use of multi core processors.
* Simple: Telepathy takes care of everything. All you need to do is call Connect/GetNextMessage/Disconnect.
* Message based: if we send 10 and then 2 bytes, then the other end receives 10 and then 2 bytes, never 12 at once.

MMORPGs are insanely difficult to make and we created Telepathy so that we would never have to worry about low level Networking again.

# What about...
* Async Sockets: didn't perform better in our benchmarks.
* ConcurrentQueue: .NET 3.5 compatibility is important for Unity. Wasn't faster than our SafeQueue anyway.
* UDP vs. TCP: Minecraft and World of Warcraft are two of the biggest multiplayer games of all time and they both use TCP networking. There is a reason for that.

# Using the Telepathy Server
```C#
// create and start the server
Telepathy.Server server = new Telepathy.Server();
server.Start(1337);

// grab all new messages. do this in your Update loop.
Telepathy.Message msg;
while (server.GetNextMessage(out msg))
{
    switch (msg.eventType)
    {
        case Telepathy.EventType.Connect:
            Console.WriteLine(msg.connectionId + " Connected");
            break;
        case Telepathy.EventType.Data:
            Console.WriteLine(msg.connectionId + " Data: " + BitConverter.ToString(msg.data));
            break;
        case Telepathy.EventType.Disconnect:
            Console.WriteLine(msg.connectionId + " Disconnected");
            break;
    }
}

// send a message to client with connectionId = 0 (first one)
server.Send(0, new byte[]{0x42, 0x1337});

// stop the server when you don't need it anymore
server.Stop();
```

# Using the Telepathy Client
```C#
// create and connect the client
Telepathy.Client Client = new Telepathy.Client();
client.Connect("localhost", 1337);

// grab all new messages. do this in your Update loop.
Telepathy.Message msg;
while (client.GetNextMessage(out msg))
{
    switch (msg.eventType)
    {
        case Telepathy.EventType.Connect:
            Console.WriteLine("Connected");
            break;
        case Telepathy.EventType.Data:
            Console.WriteLine("Data: " + BitConverter.ToString(msg.data));
            break;
        case Telepathy.EventType.Disconnect:
            Console.WriteLine("Disconnected");
            break;
    }
}

// send a message to server
client.Send(new byte[]{0xFF});

// disconnect from the server when we are done
client.Disconnect();
```

# Unity Integration
Here is a very simple MonoBehaviour script for Unity. It's really just the above code with logging configured for Unity's Debug.Log:
```C#
using System;
using UnityEngine;

public class SimpleExample : MonoBehaviour
{
    Telepathy.Client client = new Telepathy.Client();
    Telepathy.Server server = new Telepathy.Server();

    void Awake()
    {
        // update even if window isn't focused, otherwise we don't receive.
        Application.runInBackground = true;

        // use Debug.Log functions for Telepathy so we can see it in the console
        Telepathy.Logger.LogMethod = Debug.Log;
        Telepathy.Logger.LogWarningMethod = Debug.LogWarning;
        Telepathy.Logger.LogErrorMethod = Debug.LogError;
    }

    void Update()
    {
        // client
        if (client.Connected)
        {
            if (Input.GetKeyDown(KeyCode.Space))
                client.Send(new byte[]{0x1});

            // show all new messages
            Telepathy.Message msg;
            while (client.GetNextMessage(out msg))
            {
                switch (msg.eventType)
                {
                    case Telepathy.EventType.Connected:
                        Console.WriteLine("Connected");
                        break;
                    case Telepathy.EventType.Data:
                        Console.WriteLine("Data: " + BitConverter.ToString(msg.data));
                        break;
                    case Telepathy.EventType.Disconnected:
                        Console.WriteLine("Disconnected");
                        break;
                }
            }
        }

        // server
        if (server.Active)
        {
            if (Input.GetKeyDown(KeyCode.Space))
                server.Send(0, new byte[]{0x2});

            // show all new messages
            Telepathy.Message msg;
            while (server.GetNextMessage(out msg))
            {
                switch (msg.eventType)
                {
                    case Telepathy.EventType.Connected:
                        Console.WriteLine(msg.connectionId + " Connected");
                        break;
                    case Telepathy.EventType.Data:
                        Console.WriteLine(msg.connectionId + " Data: " + BitConverter.ToString(msg.data));
                        break;
                    case Telepathy.EventType.Disconnected:
                        Console.WriteLine(msg.connectionId + " Disconnected");
                        break;
                }
            }
        }
    }

    void OnGUI()
    {
        // client
        GUI.enabled = !client.Connected;
        if (GUI.Button(new Rect(0, 0, 120, 20), "Connect Client"))
            client.Connect("localhost", 1337);

        GUI.enabled = client.Connected;
        if (GUI.Button(new Rect(130, 0, 120, 20), "Disconnect Client"))
            client.Disconnect();

        // server
        GUI.enabled = !server.Active;
        if (GUI.Button(new Rect(0, 25, 120, 20), "Start Server"))
            server.Start(1337);

        GUI.enabled = server.Active;
        if (GUI.Button(new Rect(130, 25, 120, 20), "Stop Server"))
            server.Stop();

        GUI.enabled = true;
    }

    void OnApplicationQuit()
    {
        // the client/server threads won't receive the OnQuit info if we are
        // running them in the Editor. they would only quit when we press Play
        // again later. this is fine, but let's shut them down here for consistency
        client.Disconnect();
        server.Stop();
    }
}
```
Make sure to enable 'run in Background' for your project settings, which is a must for all multiplayer games.
Then build it, start the server in the build and the client in the Editor and press Space to send a test message.

# Benchmarks
**Real World**<br/>
Telepathy is constantly tested in production with [uMMORPG](https://www.assetstore.unity3d.com/#!/content/51212).
We [recently tested](https://docs.google.com/document/d/e/2PACX-1vQqf_iqOLlBRTUqqyor_OUx_rHlYx-SYvZWMvHGuLIuRuxJ-qX3s8JzrrBB5vxDdGfl-HhYZW3g5lLW/pub#h.h4wha2mpetsc) 100+ players all broadcasting to each other in the worst case scenario, without issues.

We had to stop the test because we didn't have more players to spawn clients.<br/>
The next huge test will come soon...

**Connections Test**<br/>
We also test only the raw Telepathy library by spawing 1 server and 1000 clients, each client sending 100 bytes 14 times per second and the server echoing the same message back to each client. This test should be a decent example for an MMORPG scenario and allows us to test if the raw Telepathy library can handle it.

Test Computer: 2015 Macbook Pro with a 2,2 GHz Intel Core i7 processor.<br/>
Test Results:<br/>

| Clients | CPU Usage | Ram Usage | Bandwidth Client+Server  | Result |
| ------- | ----------| --------- | ------------------------ | ------ |
|   128   |        7% |     26 MB |         1-2 MB/s         | Passed |
|   500   |       28% |     51 MB |         3-4 MB/s         | Passed |
|  1000   |       42% |     75 MB |         3-5 MB/s         | Passed |

_Note: results will be significantly better on a really powerful server. Tests will follow._

The Connections Test can be reproduced with the following code:<br/>
```C#
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Telepathy;

public class Test
{
    static void Main()
    {
        // start server
        Server server = new Server();
        server.Start(1337);
        int serverFrequency = 60;
        Thread serverThread = new Thread(() =>
        {
            Logger.Log("started server");
            while (true)
            {
                // reply to each incoming message
                Message msg;
                while (server.GetNextMessage(out msg))
                {
                    if (msg.eventType == EventType.Data)
                        server.Send(msg.connectionId, msg.data);
                }

                // sleep
                Thread.Sleep(1000 / serverFrequency);
            }
        });
        serverThread.IsBackground = false;
        serverThread.Start();

        // start n clients and get queue messages all in this thread
        int clientAmount = 1000;
        string message = "Sometimes we just need a good networking library";
        byte[] messageBytes = Encoding.ASCII.GetBytes(message);
        int clientFrequency = 14;
        List<Client> clients = new List<Client>();
        for (int i = 0; i < clientAmount; ++i)
        {
            Client client = new Client();
            client.Connect("localhost", 1337);
            clients.Add(client);
            Thread.Sleep(15);
        }
        Logger.Log("started all clients");

        while (true)
        {
            foreach (Client client in clients)
            {
                // send 2 messages each time
                client.Send(messageBytes);
                client.Send(messageBytes);

                // get new messages from queue
                Message msg;
                while (client.GetNextMessage(out msg))
                {
                }
            }

            // client tick rate
            Thread.Sleep(1000 / clientFrequency);
        }
    }
}
```