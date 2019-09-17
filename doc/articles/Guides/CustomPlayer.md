# Customizable Players

In many games you want to customize character creation.  You may want to pick the color of the hair, eyes, skin, height, race, etc.

By default Mirror will instantiate the player for you, which might prevent you from customizing it. However Mirror provides the option of overriding player creation and customize it.

1) Create a class that extends NetworkManager if you have not done so. For example:
```cs
public class MMONetworkManager : NetworkManager
{
    ...
}
```
and use it as your Network manager.
2) Open your Network Manager and disable the "Auto Create Player" boolean.
3) Create a message that describes your player.  For example:
```cs
public class CreateMMOCharacterMessage : MessageBase
{
    public Race race;
    public string name;
    public Color hairColor;
    public Color eyeColor
    ...
}
```
4) Create your player prefabs (as many as you need) and add them to the "Register Spawnable Prefabs" in your Network Manager
5) Send your message and register a player
```cs
public class MMONetworkManager : NetworkManager
{
    public override void OnStartServer()
    {
        base.OnStartServer();

        NetworkServer.RegisterHandler<CreateMMOCharacterMessage>(OnCreateCharacter);
    }

    private void OnCreateCharacter(NetworkConnection conn, CreateMMOCharacterMessage message)
    {
        // use message data to locate the prefab 
        // and intitialize whatever attribute you want
        Race race = message.race;
        String name = message.name;
        Color hairColor = message.hairColor;
        Color eyeColor = message.eyeColor;

        GameObject player = Instantiate(somePrefab);

        // call this to use this gameobject as the 
        // primary controller
        NetworkServer.AddPlayerForConnection(conn, player);
    }

    public override void OnClientConnect(NetworkConnection conn)
    {
        base.OnClientConnect(conn);

        // you can send the message here,  or wherever else you want
        CreateMMOCharacterMessage characterMessage = new CreateMMOCharacterMessage() 
        {
            race = Race.Elvish,
            name = "Joe Gaba Gaba",
            hairColor = Color.Red,
            eyeColor = Color.White
        }

        conn.Send(characterMessage);
    }
}
```
