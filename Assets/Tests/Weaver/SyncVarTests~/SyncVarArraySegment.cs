using Mirror;
using System;

namespace SyncVarTests.SyncVarArraySegment
{
    class SyncVarArraySegment : NetworkBehaviour
    {
       [SyncVar]
       public ArraySegment<int> data;
    }
}
