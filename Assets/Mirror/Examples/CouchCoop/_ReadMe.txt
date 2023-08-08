# Couch Co-Op Example

Open scene: SceneCouchCoop

- Start game, click UI button Add Player, to add local couch players, add as few or as many local couch players as you want.
(only 4 keyboard inputs have been setup for this example, for more players, add more controls).
Join via another client, localhost, LAN or across internet, and add remote couch players.
(no forced amount, can be any combination, example, 1 vs 1, 2 vs 2, 2 vs 4, 99 vs 20)

- Jump keys are numbers 1, 2, 3, 4, depending on which player you are.
Then AD, FG, HJ, KL for movement, all can be customised on the CouchPlayerManager script.
Everyone joint uses arrow keys and space bar for quick fun testing of all local couch players.
(something you would remove for release)

- Locate Prefab: CouchPlayerManager
Set your custom controls here, the max couch players that can be spawned will depend on control key array lengths.