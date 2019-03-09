using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        public class Potato0
        {
            public int hamburgers = 17;
            public Potato1 p1;
        }

        public class Potato1
        {
            public int hamburgers = 18;
            public Potato0 p0;
        }

        [SyncVar]
        Potato0 recursionTime;
    }
}
