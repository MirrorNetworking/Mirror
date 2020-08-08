using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    static class MirrorTestPlayer 
    {
        [Client]
        static void ClientOnlyMethod() {}
    }
}
