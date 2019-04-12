# Additive Scenes Example

IMPORTANT: Make sure you have a layer in project settings called Player for this example to work well.

In Build Settings, remove all scenes and add all of the scenes from the Examples\AdditiveScenes\Scenes folder in the following order:

- MainScene
- SubScene

File -> Build and Run

Start up to 3 built instances:  These will all be client players.

Open the MainScene in the Editor and press Play

Click LAN Host in the editor: This will be the host and the 4th player.  You can also use LAN Server if you prefer.

Click LAN Client in the built instances.

- WASDQE keys to move & turn your player capsule.
- There are objects in the corners of the scene hidden by Proximity Checkers.
- The big area in the middle is where the subscene will be loaded when you get near the shelter.
- There are also networked objects inside the subscene, also with Proximity Checkers.
- Since subscenes are only loaded for individual clients, other clients that are outside the middle Zone won't see what those in the subscene can see.
- If you play a built instance as Host or Server and play as client in the editor, you'll see the subscene content load and unload in the hierarchy as you move in and out of the middle Zone.
