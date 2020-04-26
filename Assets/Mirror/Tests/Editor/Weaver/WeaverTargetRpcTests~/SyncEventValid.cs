using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverTargetRpcTests.SyncEventValid
{
    class SyncEventValid : NetworkBehaviour
    {
        public delegate void MySyncEventDelegate();

        [SyncEvent]
        public event MySyncEventDelegate EventDoCoolThingsWithExcitingPeople;
    }
}
