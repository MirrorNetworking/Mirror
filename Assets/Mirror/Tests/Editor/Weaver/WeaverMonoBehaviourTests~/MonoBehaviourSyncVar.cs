using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.MonoBehaviourSyncVar
{
    class MonoBehaviourSyncVar : MonoBehaviour
    {
        [SyncVar]
        int potato;
    }
}
