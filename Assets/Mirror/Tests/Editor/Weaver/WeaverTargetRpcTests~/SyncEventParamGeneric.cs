using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverTargetRpcTests.SyncEventParamGeneric
{
    class SyncEventParamGeneric : NetworkBehaviour
    {
        public delegate void MySyncEventDelegate<T>(T amount, float dir);

        [SyncEvent]
        public event MySyncEventDelegate<int> EventDoCoolThingsWithExcitingPeople;
    }
}
