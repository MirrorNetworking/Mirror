# Actions and Communication

When you are making a multiplayer game, In addition to synchronizing the properties of networked game objects, you are likely to need to send, receive, and react to other pieces of information - such as when the match starts, when a player joins or leaves the match, or other information specific to your type of game, for example a notification to all players that a flag has been captured in a “capture-the-flag” style game.

Within the Mirror networking High-Level API there are three main ways to communicate this type of information.

## Remote Actions

Remote actions allow you to call a method in your script across the network. You can make the server call methods on all clients or individual clients specifically. You can also make clients call methods on the server. Using remote actions, you can pass data as parameters to your methods in a very similar way to how you call methods in local (non-multiplayer) projects.

## Networking Callbacks

Networking callbacks allow you to hook into built-in Mirror events which occur during the course of the game, such as when players join or leave, when game objects are created or destroyed, or when a new Scene is loaded. There are two types of networking callbacks that you can implement:

-   Network manager callbacks, for callbacks relating to the network manager itself (such as when clients connect or disconnect)
-   Network behaviour callbacks, for callbacks relating to individual networked game objects (such as when its Start function is called, or what this particular game object should do if a new player joins the game)

## Network Messages

Network messages are a “lower level” approach to sending messages (although they are still classed as part of the networking “High level API”). They allow you to send data directly between clients and the server using scripting. You can send basic types of data (int, string, etc) as well as most common Unity types (such as Vector3). Since you implement this yourself, these messages are not associated directly with any particular game objects or Unity events - it is up to you do decide their purpose and implement them!
