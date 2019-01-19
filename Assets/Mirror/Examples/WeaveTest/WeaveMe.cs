using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public partial class LoginMsg : MessageBase
{
    public static short MsgId = 1000;
    public string account;
    public string password;
    public string version;
}

public class Weaveme : MonoBehaviour
{
    void Start()
    {
        Debug.Log("BREAK");

        // serialize
        LoginMsg msg = new LoginMsg();
        msg.version = "42";
        NetworkWriter writer = new NetworkWriter();
        msg.Serialize(writer);

        // deserialize
        LoginMsg other = new LoginMsg();
        other.Deserialize(new NetworkReader(writer.ToArray()));
        Debug.Log("version=" + other.version + " expected=" + msg.version);

        //NetworkMessage netmsg = new NetworkMessage();
        //netmsg.reader =
    }
}
