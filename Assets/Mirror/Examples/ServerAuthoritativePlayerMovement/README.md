# Server Authoritative Player Movement example

Show one possible approach to handling player movement on the server.  In short, player input is sent from the client to the server via a Command.
The server applies the player input to the player's gameobject on the server itself using rigidbody / force, and once the game object moves,
SyncVars are updated to tell the clients where to move the player gameobjects.

1) Open the scene in unity
2) File -> Build and run   as standalone 
3) When the standalone starts,  click on Host
4) the standalone starts as both a client and a server and starts listening to port 7777
5) File -> Build and run   as standalone, this time run as Client
6) File -> Build and run   as standalone, run another Client, you now have 3 players (one of which is on the host server)
7) Using WASD on the keyboard, if you move one of the clients, note how the server moves first and the updates the clients 
   using SyncVars for ClientMoveToPosition and ClientMoveToRotation.  The host server runs ahead of the clients slightly but each client is
   kept in sync with each other
8) You can also use an Xbox controller and override the player rotation using the right stick (see folder Right Joystick Inputs Image for help)
