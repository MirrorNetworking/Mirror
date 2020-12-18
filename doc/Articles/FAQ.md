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
Below are some tips as to why, and how you can add a host migation-like alternative.
  - Dedicated hosts should rarely ever be closed (If you are doing games that need to stay open such as MMO's).
  - Short arena maps, take players back to the games list/matchmaker, so they can just join another, nice and simple.

The work around is to basically fake the host migration, store info of a backup host on players games, switch everyone in the game to this, reset all data back to how it was before the original host dissappeared.
- Test players connections when they join, find one with unblocked ports, and decent ping/latency.
- Send this players data (IP and Port) on all connected players games.
- Save various player info, either locally or on that backup host, such as player positions, health etc
- Upon disconnection from server, call a function to connect to the backup hoster StartClient( BackupIP - BackupPort ).
- As the scenes will most likely reset,along with players respawning, you now need to set player position back to your stored one that was saved either via checkpoints, or in the disconnect detection callback.
- Cover all this up with a UI, saying please wait.

Depending on what your game is like, it'l either be easy or difficult to add the work-around.
An example of these are:
- (easier) A game only needing player position data such as "Fall Guys".
- (difficult) Forge of Empires, a game where created objects are placed, soldiers, vehicles, various other crafts and upgrades, all with their own levels/stats.

</details>
