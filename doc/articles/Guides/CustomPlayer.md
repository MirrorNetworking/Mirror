# Customizable Players

Many games need character customization.  You may want to pick the color of the hair, eyes, skin, height, race, etc.

By default Mirror will instantiate the player for you. While that is convenient, it might prevent you from customizing it. Mirror provides the option of overriding player creation and customize it.

1) Create a class that extends NetworkManager if you have not done so. For example:
```cs
public class MMONetworkManager : NetworkManager
{
    ...
}
```
and use it as your Network manager.  
2) Open your Network Manager in the inspector and disable the "Auto Create Player" boolean.  
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
4) Create your player prefabs (as many as you need) and add them to the "Register Spawnable Prefabs" in your Network Manager,  or add a single prefab to the player prefab field in the inspector.
5) Send your message and register a player
```cs
public class MMONetworkManager : NetworkManager
{
    public override void OnStartServer()
    {
        base.OnStartServer();

        NetworkServer.RegisterHandler<CreateMMOCharacterMessage>(OnCreateCharacter);
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

    private void OnCreateCharacter(NetworkConnection conn, CreateMMOCharacterMessage message)
    {
        // playerPrefab is the one assigned
        // in the inspector in Network Manager
        // but you can use different prefabs
        // per race for example
        GameObject gameobject = Instantiate(playerPrefab);

        // apply data from the message
        // however appropriate for your game
        // Typically Player would be a component you write
        // with syncvars or properties
        Player player = gameobject.GetComponent<Player>();
        player.hairColor = message.hairColor;
        player.eyeColor = message.eyeColor;
        player.name = message.name;

        // call this to use this gameobject as the 
        // primary controller
        NetworkServer.AddPlayerForConnection(conn, player);
    }
}
```
