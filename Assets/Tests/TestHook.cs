using UnityEngine;
using Mirror;

public class TestHook : NetworkBehaviour
{
    // the syncvar
    [SyncVar(hook="OnTest")] GameObject test;
    [SyncVar(hook="OnTest2")] int test2;

    void OnTest(GameObject newValue)
    {
    }

    void OnTest2(int newValue)
    {
    }
}
