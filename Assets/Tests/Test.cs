using UnityEngine;
using Mirror;

public class Test : NetworkBehaviour
{
    // the syncvar
    [SyncVar] GameObject test;
    [SyncVar] NetworkIdentity test2;

    // a function that uses it
    /*void Update()
    {
        // read and write once
        Debug.Log(test.name);
        GameObject read = test;
        test = gameObject;

        // read and write once
        Debug.Log(test2.name);
        NetworkIdentity read2 = test2;
        test2 = netIdentity;
    }*/
}
