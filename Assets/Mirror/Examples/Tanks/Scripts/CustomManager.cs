using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class CustomManager : NetworkManager
{
    public List<MatchData> matches = new List<MatchData>();
    public string Localplayername;
    public GameObject playerTankPrefab, playerConnPrefab;

    public override void OnStartServer()
    {
        base.OnStartServer();
        NetworkServer.RegisterHandler<CreateMessage>(Createplayer);
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        CreateMessage ms = new CreateMessage
        {
            playername = Localplayername
        };
        NetworkClient.Send(ms);
    }

    [ServerCallback]
    void Createplayer(NetworkConnectionToClient conn, CreateMessage message)
    {
        //first Create dumy player Obj
        Debug.Log("Receive Create Player MSG");
        GameObject playerConn = GameObject.Instantiate(playerConnPrefab);
        NetworkServer.Spawn(playerConn);
        playerConn.name = "conn" + message.playername;
        NetworkServer.AddPlayerForConnection(conn, playerConn);


        //then replace it with tank obj
        //also check it if this player name already on match
        foreach (MatchData item in matches)
        {
            //if yes dont make new tank,and REplace dumy player Obj with already
            //spawned tank Obj in server based on player name
            if (item.playerName == message.playername.Replace(" ", ""))
            {
                item.connObj=playerConn;
                NetworkServer.ReplacePlayerForConnection(conn, item.playerObj, false);
                return;
            }
        }

        //if no add this new player to matchdata and spawn tank for it and also
        //REplace dumy player Obj with already spawned tank Obj in server
        GameObject playerTank = GameObject.Instantiate(playerTankPrefab);
        playerTankPrefab.name = "tank" + message.playername;
        NetworkServer.Spawn(playerTank);
        MatchData md = new MatchData();
        md.playerName = message.playername;
        md.playerObj = playerTank;
        md.connObj = playerConn;
        matches.Add(md);
        NetworkServer.ReplacePlayerForConnection(conn, playerTank, false);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        foreach (MatchData item in matches)
        {
             if (item.playerObj == conn.identity.gameObject)
            {
                Debug.Log(conn.identity.gameObject.name);
                NetworkServer.ReplacePlayerForConnection(conn, item.connObj, false);
                item.connObj=null;
            }
        }
        base.OnServerDisconnect(conn);
    }

}
public class MatchData
{
    public string playerName;
    public GameObject connObj;
    public GameObject playerObj;
}
public struct CreateMessage : NetworkMessage
{
    public string playername;
}
