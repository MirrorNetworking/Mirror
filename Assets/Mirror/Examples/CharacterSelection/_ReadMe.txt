# Character Selection Example

Three scenes, each provide a slightly different approach towards selecting characters and customisations.

1: MirrorCharacterSelectionPreScene
This is an offline scene, allows players to select data, which is then saved and passed across to other scenes using static variables.
Once selected, the map loads "MirrorCharacterSelection", press Start Host, or Client to play. (remember to add scenes to build settings)

2: MirrorCharacterSelection
This scene spawns with a character (randomised), and players have an option using the UI to change this.

3: MirrorCharacterSelectionNoCharacter
Spawns an empty player prefab, then players chose which character using the UI.