using Mirror;
using System;

namespace WeaverSyncVarTests.SyncVarArraySegment
{
    class SyncVarArraySegment : NetworkBehaviour
    {
       [SyncVar]
       public ArraySegment<int> data;
    }
}
