using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.MonoBehaviourCommand
{
    class MonoBehaviourCommand : MonoBehaviour
    {
        [Command]
        void CmdThisCantBeOutsideNetworkBehaviour() {}
    }
}
