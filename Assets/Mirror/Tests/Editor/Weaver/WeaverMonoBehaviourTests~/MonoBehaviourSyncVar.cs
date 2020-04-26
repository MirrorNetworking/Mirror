using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverMonoBehaviourTests.MonoBehaviourSyncVar
{
    class MonoBehaviourSyncVar : MonoBehaviour
    {
        [SyncVar]
        int potato;
    }
}
