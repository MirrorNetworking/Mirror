using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.SyncEventValid
{
    class SyncEventValid : NetworkBehaviour
    {
        public delegate void MySyncEventDelegate();

        [SyncEvent]
        public event MySyncEventDelegate EventDoCoolThingsWithExcitingPeople;
    }
}
