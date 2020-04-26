using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.SyncEventParamGeneric
{
    class SyncEventParamGeneric : NetworkBehaviour
    {
        public delegate void MySyncEventDelegate<T>(T amount, float dir);

        [SyncEvent]
        public event MySyncEventDelegate<int> EventDoCoolThingsWithExcitingPeople;
    }
}
