# Player Game Objects

Mirror’s multiplayer HLAPI system handles player game objects differently to non-player game objects. When a new player joins the game (when a new client connects to the server), that player’s game object becomes a “local player” game object on the client of that player, and Unity associates the player’s connection with the player’s game object. Unity associates one player game object for each person playing the game, and routes networking commands to that individual game object. A player cannot invoke a command on another player’s game object, only their own.

The NetworkBehaviour class (which you derive from to create your network scripts) has a property called isLocalPlayer. On each client’s player game object, Mirror sets that property to true on the NetworkBehaviour script, and invokes the OnStartLocalPlayer() callback. This means each client has a different game object set up like this, because on each client a different game object is the one that represents the local player. The diagram below shows two clients and their local players.

![In this diagram, the circles represent the player game objects marked as the local player on each client](NetworkLocalPlayers.png)

Only the player game object that is “yours” (from your point of view as the player) has the `isLocalPlayer` flag set. Usually you should set this flag in script to determine whether to process input, whether to make the camera track the game object, or do any other client-side things that should only occur for the player belonging to that client.

Player game objects represent the player (that is, the person playing the game) on the server, and have the ability to run commands from the player’s client. These commands are secure client-to-server remote procedure calls. In this server-authoritative system, other non-player server-side game objects cannot receive commands directly from client-side game objects. This is both for security, and to reduce the complexity of building your game. By routing all incoming commands from users through the player game object, you can ensure that these messages come from the right place, the right client, and can be handled in a central location.

The Network Manager adds a player every time a client connects to the server. In some situations though, you might want it not to add players until an input event happens - such as a user pressing a “start” button on the controller. To disable automatic player creation, navigate to the Network Manager component’s Inspector and untick the Auto Create Player checkbox.
