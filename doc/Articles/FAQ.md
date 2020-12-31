# FAQ

Anything to add to this list? Please make a PR or ask in the discord.

### How do I use this feature?

<details>
  <summary>How to Send/Sync custom data types?</summary>
  
  Mirror can automatically create Serialization functions for many custom data types when your scripts are compiled.

  For example, mirror will automatically create a functions for `MyCustomStruct` so that it can be sent without any extra work.
  ```cs
  [ClientRpc]
  public void RpcDoSomething(MyCustomStruct data)
  {
      // do stuff here
  }

  struct MyCustomStruct
  {
      int someNumber;
      Vector3 somePosition;
  }
  ```

  For More details 
  - [DataTypes](https://mirror-networking.com/docs/Guides/DataTypes.html)
  - [Serialization](https://mirror-networking.com/docs/Guides/Serialization.html)
</details>


### How to Connect

<details>
  <summary>How to connect to games on same PC</summary>

  Make sure the networkAddress field on NetworkManager or the Hud is set up `localHost`
</details>

<details>
  <summary>How to connect to a different PC/Device on same network</summary>

  Set the networkAddress field to the LAN IP of the host `192.168.x.x`

  *In some cases you may need additional steps, check below*

  To check ip on Windows you can open powershell and use the `ipconfig` command, then under your current adapter (ethernet/wifi/etc) look for `IPv4 Address`

  ` IPv4 Address. . . . . . . . . . . : 192.168.x.x `
</details>

<details>
  <summary>How to connect to a different PC/Device over the internet </summary>

  Set the networkAddress field to be the IP address of the host (google 'whats my IP')

  > This section does not cover relays/dedicated vps/headless features

  For this to work, you will need to do **some** of the following, most of these depend on your set up and router

  - **Port forward**: you'll have to login your Router
    - Either forward your game port (default is 7777) for your PC's local IP. (192.168.1.20 for example) 
    - Or the quick (but less safe) add that local IP to DMZ.

  - **PC Firewalls**: 
    - You can turn it off for a quick test (And turn it back on later)
    - manually allow the editor and any builds you create it in firewalls settings.

  - Try from a build rather than the Unity Editor
  
  - Some anti virus/phones may have additional blocking.
    - You can turn it off for a quick test (And turn it back on later)
  
  - In rare cases ISPs or companies/schools block ports and connections, this is harder to adjust yourself.

  If you need more help it is best to google for guide for your setup and router.

  An alternative to the above is to use a dedicated server (vps) or use a relay.
</details>


### Host Migration

<details>
  <summary>Host migration alternatives and work-around.</summary>

Host migration as of writing is not built into Mirror, and it is best to avoid Host Migration completely if you can.
Below are some tips as to why, and how you can add a host migration-like alternative.
  - Dedicated hosts should rarely ever be closed (If you are doing games that need to stay open such as MMO's).
  - Short arena maps, take players back to the games list/matchmaker, so they can just join another, nice and simple.

The work around is to basically fake the host migration, store info of a backup host on players game, upon disconnection, reconnect everyone in the game to that new host, then restore positions and variable data back to how it was before the original host dissappeared.
- Test players connections when they join, find one with unblocked ports, and decent ping/latency.
- Send this players data (IP and Port) on all connected players games.
- Save various player info, either locally or on that backup host, such as player positions, health etc
- Upon disconnection from server, call a function to connect to the backup hoster StartClient( BackupIP - BackupPort ).
- As the scenes will most likely reset, along with players respawning, you now need to set player position back to your stored one that was saved either via checkpoints, or in the disconnect detection callback.
- Cover all this up with a UI, saying please wait (optional, should happen in the blink of an eye).

Depending on what your game is like, it'll either be easy or difficult to add the work-around.
An example of these are:
- (easier) A game only needing player position data such as "Fall Guys".
- (difficult) Forge of Empires, a game where created objects are placed, soldiers, vehicles, various other crafts and upgrades, all with their own levels/stats.

</details>

### Master / List Servers and Simple Matchmaker

<details>
  <summary>A database of world-wide registered host data.</summary>
    
All the hosts, dedicated or player hosts, get added into a list database, players get the list and can choose who to join.
Using a list server means players do not have to manually enter IP addresses and Ports, it is all done behind the scenes, and works for localhost, LAN, and WAN connection types.
You can show as much, or as little data as you like to the players, such as host name, type (deathmatch), player count (45 / 50), ping, enemy difficulty, map, region etc

- Mirror Listserver: Mirror squad manage and host this for you, you pay monthly subscription, but do not have to worry about setting up or maintaining it.
- Node ListServer: Free, but you host the files yourself on an unblocked PC, like a VPS. Has a wide variety of customisable features, best option if you want self hosted dedicated games.
- Dark Reflective Mirror:  This is a list server and a relay, it is free, but you host and manage the files yourself.
Relays offer an unblocked route of traffic, but at the cost of extra latency/increased ping. This relay will test for a direct connection first, then fallback to routed traffic if that fails. Both the list server and relayed traffic is optional, you can use one part, and not the other.
This is the best option for player hosted games, where router port blocks are common.
    
Simple Matchmaker
- You can make matchmakers out of these list servers, simply hide the list to players, have them auto join a game with space. You could also filter out various requirements the player has set, for example, "USA" Region only, or "Lava Island" map.
    </details>
    
### How to get player count?

<details>
  <summary>There are a few ways to do this, each with their own unique benefit.</summary>
    
NetworkServer.connections.Count
- Socket connections, includes people without spawned prefabs, non authenticated, or that may have bugged out but during spawn, but have connected temporarily (Android users minimising game).  Only host / server can check this.

NetworkManager.singleton.numPlayers
- Number of active spawned player objects across all connections on the server.
Only host / server can check this, an example of how to sync this number to all players:
```cs
public class PlayerCounter : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnNumPlayersChanged))]
    public int currentNumOfPlayers;

    // This fires on client when value changes
    void OnNumPlayersChanged(int _, int newValue)
    {
        // Put this in a UI
        Debug.Log(newValue);
    }
```
```cs
public class NewNetworkManager : NetworkManager
{
    // Assign in inspector by dragging scene object
    // with PlayerCounter script to this field.
    public PlayerCounter playerCounter;

    public override void OnServerAddPlayer(NetworkConnection conn)
    {
        base.OnServerAddPlayer(conn);
        playerCounter.currentNumOfPlayers = numPlayers;
    }

    public override void OnServerDisconnect(NetworkConnection conn)
    {
        base.OnServerDisconnect(conn);
        playerCounter.currentNumOfPlayers = numPlayers;
    }
}
```
Find by Player tag to array.
- Is a good way to distinguish between states, by applying the gameobjects tag for certain situtations, example states 'not ready', 'is dead', 'spectator'.
Example:
```cs
public class PlayerCounter : MonoBehaviour
{
    public Text canvasPlayerCount;
    public GameObject[] playersArray;

    public void ButtonFindPlayers()
    {
       playersArray = GameObject.FindGameObjectsWithTag("Player");
       canvasPlayerCount.text = "Players: " + playersArray.Length;
    }
}
```
</details>
