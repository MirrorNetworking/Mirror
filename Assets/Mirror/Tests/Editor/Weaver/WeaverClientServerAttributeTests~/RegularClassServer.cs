using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer 
    {
        [Server]
        void ServerOnlyMethod() {}
    }
}
