using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        public delegate void MySyncEventDelegate(int amount, float dir);

        [SyncEvent]
        public event MySyncEventDelegate DoCoolThingsWithExcitingPeople;
    }
}
