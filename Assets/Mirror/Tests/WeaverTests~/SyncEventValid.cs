using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        public delegate void MySyncEventDelegate();

        [SyncEvent]
        public event MySyncEventDelegate EventDoCoolThingsWithExcitingPeople;
    }
}
