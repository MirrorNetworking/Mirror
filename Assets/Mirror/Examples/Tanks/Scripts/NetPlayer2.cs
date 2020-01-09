using UnityEngine;
using Mirror;

public class NetPlayer2 : NetworkBehaviour
{
    [SyncVar(hook = nameof(Hook))]
    GameObject go = null;

    public override void OnStartAuthority() {
        Debug.LogWarning("1 Authority started");
        CmdSet();
    }

    [Command]
    void CmdSet()
    {
        Debug.LogWarning("2 CmdSet: " + go);
        // set go to this. setting it will call the Hook immediately.
        // -> the hook will call CmdCheck
        // -> CmdCheck returns
        // -> Hook() returns
        // then we are back in CmdSet
        go = gameObject;
    }

    void Hook(GameObject oldGo, GameObject newGo)
    {
        Debug.LogWarning("3 Hook old:" + oldGo + " new:" + newGo);
        CmdCheck(newGo);
    }

    [Command]
    void CmdCheck(GameObject newGoX)
    {
        Debug.LogWarning("4 CmdCheck x=" + go + " newX=" + newGoX);
    }
}
