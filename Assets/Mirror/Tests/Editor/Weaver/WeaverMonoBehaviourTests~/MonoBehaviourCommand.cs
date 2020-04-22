using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MonoBehaviourCommand : MonoBehaviour
    {
        [Command]
        void CmdThisCantBeOutsideNetworkBehaviour() {}
    }
}
