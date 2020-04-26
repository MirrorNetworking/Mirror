using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverMonoBehaviourTests.MonoBehaviourCommand
{
    class MonoBehaviourCommand : MonoBehaviour
    {
        [Command]
        void CmdThisCantBeOutsideNetworkBehaviour() {}
    }
}
