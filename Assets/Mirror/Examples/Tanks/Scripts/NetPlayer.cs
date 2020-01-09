using UnityEngine;
using Mirror;

public class NetPlayer : NetworkBehaviour
{
    [SyncVar(hook = nameof(Hook))]
    int x = 5;

    public override void OnStartAuthority() {
        Debug.LogWarning("1 Authority started");
        CmdSet();
    }

    [Command]
    void CmdSet()
    {
        Debug.LogWarning("2 CmdSet: " + x);
        // set x to 10. setting it will call the Hook immediately.
        // -> the hook will call CmdCheck
        // -> CmdCheck returns
        // -> Hook() returns
        // then we are back in CmdSet
        x = 10;
    }

    void Hook(int oldX, int newX)
    {
        Debug.LogWarning("3 Hook old:" + oldX + " new:" + newX);
        CmdCheck(newX);
    }

    [Command]
    void CmdCheck(int newX)
    {
        Debug.LogWarning("4 CmdCheck x=" + x + " newX=" + newX);
    }
}
