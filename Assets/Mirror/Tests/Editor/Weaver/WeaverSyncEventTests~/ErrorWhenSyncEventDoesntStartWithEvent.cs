using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverTargetRpcTests.SyncEventStartsWithEvent
{
    class ErrorWhenSyncEventDoesntStartWithEvent : NetworkBehaviour
    {
        public delegate void MySyncEventDelegate(int amount, float dir);

        [SyncEvent]
        public event MySyncEventDelegate DoCoolThingsWithExcitingPeople;
    }
}
